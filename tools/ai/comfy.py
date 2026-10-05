"""Client minimal pour l'API HTTP de ComfyUI (bibliothèque standard uniquement) + constructeurs de workflows.

Workflows (API « prompt graph »), repris des modèles fournis avec ComfyUI 0.37 :
  - zimage_txt2img : Z-Image Turbo (int8) texte -> image, 8 étapes.
  - qwen_edit      : Qwen-Image-Edit 2511 (int8 + LoRA Lightning 4 étapes) image + consigne -> image retouchée.
"""
import io
import json
import mimetypes
import os
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = os.environ.get("COMFY_URL", "http://127.0.0.1:8188")


class ComfyError(RuntimeError):
    pass


class Comfy:
    def __init__(self, base=BASE):
        self.base = base.rstrip("/")
        self.client_id = uuid.uuid4().hex

    # --- HTTP
    def _get(self, path, timeout=30):
        with urllib.request.urlopen(self.base + path, timeout=timeout) as r:
            return r.read()

    def _post_json(self, path, obj, timeout=60):
        req = urllib.request.Request(self.base + path, data=json.dumps(obj).encode(), headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            raise ComfyError("HTTP %d: %s" % (e.code, e.read().decode(errors="replace")[:600]))

    def wait_ready(self, timeout=240):
        end = time.time() + timeout
        while time.time() < end:
            try:
                self._get("/system_stats", timeout=5)
                return True
            except Exception:
                time.sleep(2)
        raise ComfyError("ComfyUI ne répond pas sur " + self.base)

    # --- exécution
    def queue(self, graph):
        return self._post_json("/prompt", {"prompt": graph, "client_id": self.client_id})["prompt_id"]

    def wait(self, prompt_id, timeout=900, poll=1.0):
        end = time.time() + timeout
        while time.time() < end:
            hist = json.loads(self._get("/history/" + prompt_id))
            if prompt_id in hist:
                h = hist[prompt_id]
                status = h.get("status", {})
                if status.get("status_str") == "error":
                    msgs = [m for m in status.get("messages", []) if m[0] == "execution_error"]
                    raise ComfyError("exécution en erreur : " + (json.dumps(msgs[-1][1])[:700] if msgs else "inconnue"))
                if h.get("outputs"):
                    return h["outputs"]
            time.sleep(poll)
        raise ComfyError("délai dépassé (%ds)" % timeout)

    def fetch(self, info):
        q = urllib.parse.urlencode({"filename": info["filename"], "subfolder": info.get("subfolder", ""), "type": info.get("type", "output")})
        return self._get("/view?" + q, timeout=60)

    def run(self, graph, timeout=900):
        """Exécute un graphe et renvoie la liste des images (bytes PNG) produites par ses SaveImage."""
        outputs = self.wait(self.queue(graph), timeout=timeout)
        images = []
        for node in outputs.values():
            for info in node.get("images", []):
                images.append(self.fetch(info))
        return images

    def upload(self, path, name=None):
        """Envoie une image dans le dossier input de ComfyUI et renvoie son nom."""
        name = name or os.path.basename(path)
        boundary = uuid.uuid4().hex
        ctype = mimetypes.guess_type(path)[0] or "image/png"
        with open(path, "rb") as f:
            data = f.read()
        body = io.BytesIO()
        for k, v in (("overwrite", "true"), ("type", "input")):
            body.write(("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" % (boundary, k, v)).encode())
        body.write(("--%s\r\nContent-Disposition: form-data; name=\"image\"; filename=\"%s\"\r\nContent-Type: %s\r\n\r\n" % (boundary, name, ctype)).encode())
        body.write(data)
        body.write(("\r\n--%s--\r\n" % boundary).encode())
        req = urllib.request.Request(self.base + "/upload/image", data=body.getvalue(), headers={"Content-Type": "multipart/form-data; boundary=" + boundary})
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read())["name"]


# ------------------------------------------------------------------ workflows
def zimage_txt2img(prompt, width=1024, height=1024, seed=1, steps=8, prefix="zi"):
    """Z-Image Turbo : CFG 1, pas de prompt négatif (ConditioningZeroOut), échantillonneur res_multistep."""
    return {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": "z_image_turbo_int8_convrot.safetensors", "weight_dtype": "default"}},
        "2": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_3_4b_fp8_mixed.safetensors", "type": "lumina2", "device": "default"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
        "4": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["1", 0], "shift": 3.0}},
        "5": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}},
        "6": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["5", 0]}},
        "7": {"class_type": "EmptySD3LatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "8": {"class_type": "KSampler", "inputs": {"model": ["4", 0], "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "res_multistep",
                                                  "scheduler": "simple", "positive": ["5", 0], "negative": ["6", 0], "latent_image": ["7", 0], "denoise": 1.0}},
        "9": {"class_type": "VAEDecode", "inputs": {"samples": ["8", 0], "vae": ["3", 0]}},
        "10": {"class_type": "SaveImage", "inputs": {"images": ["9", 0], "filename_prefix": prefix}},
    }


def qwen_edit(image_name, instruction, seed=1, steps=4, prefix="qe"):
    """Qwen-Image-Edit 2511 (int8) avec le LoRA Lightning 4 étapes : retouche/restyle d'une image selon une consigne."""
    return {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": "qwen_image_edit_2511_int8_convrot.safetensors", "weight_dtype": "default"}},
        "2": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["1", 0], "lora_name": "Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors", "strength_model": 1.0}},
        "3": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["2", 0], "shift": 3.1}},
        "4": {"class_type": "CFGNorm", "inputs": {"model": ["3", 0], "strength": 1.0}},
        "5": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_2.5_vl_7b_fp8_scaled.safetensors", "type": "qwen_image", "device": "default"}},
        "6": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
        "7": {"class_type": "LoadImage", "inputs": {"image": image_name}},
        "8": {"class_type": "FluxKontextImageScale", "inputs": {"image": ["7", 0]}},
        "9": {"class_type": "VAEEncode", "inputs": {"pixels": ["8", 0], "vae": ["6", 0]}},
        "10": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {"clip": ["5", 0], "prompt": instruction, "vae": ["6", 0], "image1": ["8", 0]}},
        "11": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {"clip": ["5", 0], "prompt": "", "vae": ["6", 0], "image1": ["8", 0]}},
        "12": {"class_type": "FluxKontextMultiReferenceLatentMethod", "inputs": {"conditioning": ["10", 0], "reference_latents_method": "index_timestep_zero"}},
        "13": {"class_type": "FluxKontextMultiReferenceLatentMethod", "inputs": {"conditioning": ["11", 0], "reference_latents_method": "index_timestep_zero"}},
        "14": {"class_type": "KSampler", "inputs": {"model": ["4", 0], "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple",
                                                   "positive": ["12", 0], "negative": ["13", 0], "latent_image": ["9", 0], "denoise": 1.0}},
        "15": {"class_type": "VAEDecode", "inputs": {"samples": ["14", 0], "vae": ["6", 0]}},
        "16": {"class_type": "SaveImage", "inputs": {"images": ["15", 0], "filename_prefix": prefix}},
    }
