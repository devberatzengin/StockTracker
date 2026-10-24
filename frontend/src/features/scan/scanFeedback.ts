let audioContext: AudioContext | null = null

/** Short confirmation beep via Web Audio (no audio file needed). */
export function beep() {
  try {
    audioContext ??= new AudioContext()
    const oscillator = audioContext.createOscillator()
    const gain = audioContext.createGain()
    oscillator.frequency.value = 1046
    gain.gain.value = 0.08
    oscillator.connect(gain).connect(audioContext.destination)
    oscillator.start()
    oscillator.stop(audioContext.currentTime + 0.08)
  } catch {
    // Audio is a nicety; ignore browsers that block it.
  }
}

export function vibrate() {
  navigator.vibrate?.(60)
}

/**
 * Returns a predicate that rejects the same value seen again within `windowMs`
 * (cameras report a code on every frame while it stays in view).
 */
export function createDuplicateFilter(windowMs = 2000, now: () => number = Date.now) {
  let last: { value: string; at: number } | null = null
  return (value: string) => {
    const at = now()
    const duplicate = last !== null && last.value === value && at - last.at < windowMs
    last = { value, at }
    return !duplicate
  }
}
