/**
 * Runs `run` now and then every `intervalMs`, one pass at a time. Returns the stop handle.
 *
 * `setInterval` fires whether or not the previous pass finished, and a slow database would stack
 * passes that each re-read the same rows; a pass still running when the interval fires simply skips
 * that beat. The timer is unref'd so a background job never keeps the process alive on its own.
 */
export function startPeriodic(intervalMs: number, run: () => Promise<void>): () => void {
  let running = false
  const tick = () => {
    if (running) return
    running = true
    void run().finally(() => {
      running = false
    })
  }
  const timer = setInterval(tick, intervalMs)
  timer.unref()
  tick()
  return () => clearInterval(timer)
}
