import sys
import subprocess
from pathlib import Path

YAML = r"""
behaviors:
  LabPlayer:
    trainer_type: ppo
    hyperparameters:
      batch_size: 256
      buffer_size: 2048
      learning_rate: 0.0016305687
      beta: 0.0008620446
      epsilon: 0.1366809020
      lambd: 0.9273810187
      num_epoch: 5
    network_settings:
      normalize: true
      hidden_units: 128
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    time_horizon: 256
    summary_freq: 10000
    max_steps: 2000000
engine_settings:
  time_scale: 30
  target_frame_rate: -1
"""

RUN_ID = "ppo"
CFG_NAME = "train_marl.yaml"

def write_cfg(time_scale):
    cfg_path = Path(CFG_NAME)
    cfg_path.write_text(YAML.format(time_scale=time_scale).strip() + "\n", encoding="utf-8")
    print(f"Wrote {cfg_path.resolve()}")
    return cfg_path

def normalize_map_arg(map_arg: str) -> str:
    if not map_arg:
        return "--map=Map6"
    s = map_arg.strip()
    if s.isdigit():
        return f"--map=Map{s}"
    if not s.lower().startswith("map"):
        return f"--map={s}"
    return f"--map=Map{s[3:]}"

def run(env_path, resume, num_envs, time_scale, map_arg, no_graphics, base_port):
    cfg_path = write_cfg(time_scale)
    cmd = [
        "mlagents-learn",
        str(cfg_path),
        f"--run-id={RUN_ID}",
        f"--env={env_path}",
        "--torch-device=cpu",
        f"--num-envs={num_envs}",
        f"--base-port={base_port}",
        "--width", "854",
        "--height", "480",
    ]
    if no_graphics:
        cmd.append("--no-graphics")
    cmd.append("--resume" if resume else "--force")
    cmd += ["--env-args", normalize_map_arg(map_arg)]
    print("\nCommand:\n", " ".join(cmd))
    subprocess.run(cmd, check=True)

def main():
    if len(sys.argv) < 3:
        print("Usage:")
        print(r"    python train.py train <path_to_build_exe> [num_envs] [time_scale] [map] [base_port] [--graphics]")
        print(r"    python train.py resume <path_to_build_exe> [num_envs] [time_scale] [map] [base_port] [--graphics]")
        sys.exit(1)

    mode = sys.argv[1].lower().strip()
    env_path = sys.argv[2]
    num_envs = int(sys.argv[3]) if len(sys.argv) >= 4 and not sys.argv[3].startswith("--") else 1
    time_scale = int(sys.argv[4]) if len(sys.argv) >= 5 and not sys.argv[4].startswith("--") else 20
    map_arg = sys.argv[5] if len(sys.argv) >= 6 and not sys.argv[5].startswith("--") else "Map6"
    base_port = int(sys.argv[6]) if len(sys.argv) >= 7 and not sys.argv[6].startswith("--") else 5005
    no_graphics = ("--graphics" not in sys.argv)

    if mode == "train":
        run(env_path, resume=False, num_envs=num_envs, time_scale=time_scale, map_arg=map_arg, no_graphics=no_graphics, base_port=base_port)
    elif mode == "resume":
        run(env_path, resume=True, num_envs=num_envs, time_scale=time_scale, map_arg=map_arg, no_graphics=no_graphics, base_port=base_port)
    else:
        raise ValueError("mode must be 'train' or 'resume'")

if __name__ == "__main__":
    main()