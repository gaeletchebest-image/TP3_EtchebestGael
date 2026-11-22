"""Original synthesized audio for Hand Shooter. Run with Python + numpy.

All WAVs are authored here from oscillators and seeded noise; no external recordings.
22.05 kHz mono PCM, normalized with headroom and smooth edges.
"""
from pathlib import Path
import hashlib
import math
import re
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/HandShooter/Resources/HandShooterAudio"
RATE = 22050
RNG = np.random.default_rng(20261122)


def timeline(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def tone(t, hz, end=None):
    if end is None:
        return np.sin(2 * np.pi * hz * t)
    return np.sin(2 * np.pi * (hz * t + (end - hz) * t * t / (2 * max(t[-1], 1 / RATE))))


def noise(t, smooth=1):
    n = RNG.standard_normal(len(t))
    if smooth > 1:
        n = np.convolve(n, np.ones(smooth) / smooth, mode="same") * math.sqrt(smooth)
    return n


def decay(t, rate=10):
    return np.exp(-rate * t)


def chime(notes, duration=0.4):
    t = timeline(duration)
    result = np.zeros_like(t)
    for i, hz in enumerate(notes):
        start = i * duration / (len(notes) + 1)
        local = t - start
        result += (local >= 0) * np.exp(-np.maximum(local, 0) * 14) * (
            tone(local, hz) + 0.22 * tone(local, hz * 2.003))
    return result


def write(name, data, loop=False):
    data = data - np.mean(data)
    if loop:
        # Equal endpoints; periodic material uses integer cycles per clip.
        width = min(256, len(data) // 20)
        seam = (data[:width] + data[-width:]) * 0.5
        mix = np.linspace(0, 1, width)
        data[:width] = seam * (1 - mix) + data[:width] * mix
        data[-width:] = data[-width:] * (1 - mix) + seam * mix
        data[0] = data[-1] = 0
        # Flatten the seam's slope too, so Unity's output-rate resampler stays continuous.
        edge = 128
        blend = np.linspace(0, 1, edge)
        blend = blend * blend * (3 - 2 * blend)
        endpoint = data[0]
        data[:edge] = endpoint * (1 - blend) + data[:edge] * blend
        data[-edge:] = data[-edge:] * (1 - blend) + endpoint * blend
    else:
        ramp = min(round(RATE * 0.006), len(data) // 4)
        data[:ramp] *= np.linspace(0, 1, ramp)
        data[-ramp:] *= np.linspace(1, 0, ramp)
    peak = np.max(np.abs(data))
    data = np.tanh(data / max(peak, 0.001) * 1.3)
    data *= 0.72 / max(np.max(np.abs(data)), 0.001)
    path = DEST / f"{name}.wav"
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes((data * 32767).astype("<i2").tobytes())
    meta = path.with_suffix(".wav.meta")
    if not meta.exists():
        guid = hashlib.md5(f"HandShooterAudio/{name}".encode()).hexdigest()
        meta.write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")


def music(combat):
    # Eight bars, 120 BPM, D-minor. Both layers share a 16-second period.
    t = timeline(16)
    result = np.zeros_like(t)
    chords = ((146.832, 174.614, 220), (130.813, 164.814, 195.998),
              (116.541, 146.832, 174.614), (130.813, 164.814, 195.998))
    for bar in range(8):
        start = bar * 2
        local = t - start
        env = np.where((local >= 0) & (local < 2), np.sin(np.pi * np.clip(local / 2, 0, 1)) ** 0.6, 0)
        chord = chords[(bar // 2) % 4]
        for hz in chord:
            result += env * (tone(t, hz) + 0.2 * tone(t, hz * 2)) * (0.10 if combat else 0.16)
        for beat in range(4):
            tick = local - beat * 0.5
            on = tick >= 0
            x = np.maximum(tick, 0)
            bass = chord[0] / 2
            result += on * decay(x, 7) * tone(x, bass) * (0.35 if combat else 0.13)
            if combat:
                result += on * decay(x, 28) * np.sin(2 * np.pi * (45 * x + 2 * (1 - np.exp(-35 * x)))) * 0.65
                if beat % 2:
                    result += on * decay(x, 35) * noise(t) * 0.18
                for off in (0, 0.25):
                    hat = np.maximum(tick - off, 0)
                    result += (tick >= off) * decay(hat, 75) * (noise(t) - noise(t, 8)) * 0.055
    return result


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    source = (ROOT / "Assets/HandShooter/Runtime/ShooterAudioSettings.cs").read_text()
    cues = re.search(r"public enum SoundCue\s*\{(.*?)\}", source, re.S)[1]
    names = [n.strip() for n in cues.split(",") if n.strip()]
    melodic = {
        "DashReady": [880, 1320], "FuelLow": [440, 330], "ReloadEnd": [440, 660],
        "TankPickup": [523, 784], "RefuelStart": [262, 392], "RefuelEnd": [392, 523, 784],
        "FingerToggle": [640], "ModeChange": [440, 554, 660], "Invalid": [180, 140],
        "PresetSave": [659, 880, 1047], "PresetEquip": [523, 659],
        "HandEquip": [196, 294, 392, 587], "ThumbEquip": [294, 392, 587, 784],
        "Unlock": [392, 494, 587, 784], "EnemyAlert": [390, 520],
        "SniperLock": [1200, 1200, 1600], "DoorUnlock": [330, 494], "DoorLock": [247, 165],
        "CombatStart": [196, 185, 147], "CombatClear": [294, 392, 587],
        "ChallengeSuccess": [523, 659, 784], "ChallengeFail": [294, 220],
        "TargetRespawn": [330, 494], "TimerTick": [880], "Timeout": [330, 247, 165],
        "LevelExit": [294, 392, 587, 784], "MenuOpen": [330, 494], "MenuClose": [494, 330],
        "UiSelect": [950], "UiConfirm": [660, 880], "UiCancel": [494, 330],
        "Save": [523, 659, 1047], "Load": [392, 523, 784], "Delete": [330, 220],
        "Error": [160, 120], "Inspect": [660, 990], "Pin": [880, 1175], "Restart": [330, 440, 660]
    }
    looping = {"GrenadeFuse", "ReloadLoop", "RefuelLoop", "StationHum", "RobotMove",
               "SniperCharge", "KamikazeChase", "ProjectileFlight", "PulseFlight", "LiftMotor",
               "IndustrialAmbience", "MusicAmbient", "MusicCombat"}
    for index, name in enumerate(names):
        if name.startswith("Music"):
            write(name, music(name == "MusicCombat"), True)
            continue
        duration = 2 if name in looping else 0.45
        if "Explosion" in name or name == "Death": duration = 1.3
        if name in ("UiSelect", "FingerToggle", "TimerTick"): duration = 0.09
        t = timeline(duration)
        if name in melodic:
            data = chime(melodic[name], duration)
        elif name in ("RayFire", "HunterFire", "SniperFire", "TurretFire", "PulseEmit", "ProjectileFire"):
            hz = {"RayFire": 1400, "HunterFire": 760, "SniperFire": 2200,
                  "TurretFire": 310, "PulseEmit": 520, "ProjectileFire": 210}[name]
            data = tone(t, hz, hz * 0.12) * decay(t, 15) + noise(t, 3) * decay(t, 32) * 0.4
            data += tone(t, 70, 35) * decay(t, 16) * 0.5
        elif "Explosion" in name or name == "EnemyDestroy":
            data = noise(t, 18) * decay(t, 5) + noise(t) * decay(t, 13) * 0.3
            data += tone(t, 90, 24) * decay(t, 7)
            data += tone(t, 2700, 600) * decay(t, 22) * 0.12
        elif name in ("StepSolid", "StepMetal", "Land", "GrenadeLand", "WallHit", "TargetHit", "EnemyImpact", "PulseImpact"):
            data = noise(t, 6) * decay(t, 38) + tone(t, 95, 40) * decay(t, 40)
            if name in ("StepMetal", "GrenadeLand", "WallHit"):
                data += (tone(t, 670) + tone(t, 1033) * 0.3) * decay(t, 22) * 0.35
        elif name in ("ShieldHit", "ShieldBreak"):
            data = (tone(t, 1600, 220) + tone(t, 2317, 340) * 0.5) * decay(t, 14)
            if name == "ShieldBreak": data += noise(t) * decay(t, 8) * 0.65
        elif name in ("Dash", "GrenadeThrow"):
            data = noise(t, 10) * np.sin(np.pi * t / duration) ** 2 * decay(t, 8)
            data += tone(t, 260, 80) * decay(t, 16) * 0.3
        elif name in ("DoorOpen", "DoorClose", "DoorStop", "LiftStart", "LiftStop", "ReloadStart"):
            data = noise(t, 25) * decay(t, 10) + tone(t, 100, 45) * decay(t, 12)
            data += (tone(t, 360) + tone(t, 510)) * decay(t, 17) * 0.2
            if name == "DoorOpen": data += tone(t, 80, 220) * decay(t, 7) * 0.4
        elif name == "Death":
            data = tone(t, 720, 30) * decay(t, 3) + noise(t) * decay(t, 9) * 0.6
            data *= 0.7 + 0.3 * np.sin(2 * np.pi * 27 * t)
        elif name in looping:
            if name in ("GrenadeFuse", "KamikazeChase"):
                gate = np.maximum(0, np.sin(2 * np.pi * (4 if name == "GrenadeFuse" else 3) * t)) ** 8
                data = tone(t, 1400 if name == "GrenadeFuse" else 240) * gate
            elif name == "SniperCharge":
                data = tone(t, 420) * (0.7 + 0.3 * tone(t, 8)) + tone(t, 840) * 0.25
            elif name == "IndustrialAmbience":
                data = tone(t, 50) * 0.3 + tone(t, 100) * 0.12 + noise(t, 140) * 0.13
            elif name == "StationHum":
                data = tone(t, 120) * 0.4 + tone(t, 180) * 0.2 + noise(t, 80) * 0.1
            else:
                hz = {"ReloadLoop": 130, "RefuelLoop": 240, "RobotMove": 80,
                      "ProjectileFlight": 320, "PulseFlight": 560, "LiftMotor": 60}.get(name, 150)
                data = (tone(t, hz) + tone(t, hz * 2) * 0.3) * (0.75 + 0.25 * tone(t, 6))
                data += noise(t, 35) * 0.15
        elif name == "SniperCancel":
            data = tone(t, 840, 160) * decay(t, 9)
        elif name == "RefuelCancel":
            data = chime([330, 262], duration) + noise(t, 30) * decay(t, 20) * 0.1
        elif name == "Empty":
            data = noise(t) * decay(t, 85) + tone(t, 125) * decay(t, 70)
        else:
            raise ValueError(f"No sound recipe for {name}")
        write(name, data, name in looping)
    print(f"Generated {len(names)} original clips in {DEST}")


if __name__ == "__main__":
    main()
