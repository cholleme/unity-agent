# To setup the server

Downoad LLAMACPP executables: https://github.com/ggml-org/llama.cpp/releases
Download GPT-OSS-20B quant: https://huggingface.co/unsloth/gpt-oss-20b-GGUF the batch file uses the gpt-oss-20b-Q8_0 but you may download a smaller one if you have less GPU memory free.
- Place both in this folder.
- Run StartServerGpt.bat (if you downloded smaller one be sure to update the path)
- Visit http://127.0.0.1:8080/ to test everything works