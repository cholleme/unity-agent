set CUDA_VISIBLE_DEVICES=0
llama-server -m gpt-oss-20b-Q8_0.gguf --jinja -ngl 99 --threads -1 --ctx-size 16384 --temp 1.0 --top-p 1.0 --top-k 0