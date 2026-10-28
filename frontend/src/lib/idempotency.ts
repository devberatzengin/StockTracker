import { useRef } from 'react'

export function newIdempotencyKey(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  // randomUUID needs a secure context; fall back to random bytes elsewhere.
  return Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('')
}

/**
 * One key per submission: a retry of the same submission (double click, network error) sends the same
 * key, so the server runs it once; after a success the next submission gets a new key.
 */
export function useIdempotencyKey() {
  const key = useRef<string | null>(null)
  return {
    get: () => (key.current ??= newIdempotencyKey()),
    renew: () => {
      key.current = null
    },
  }
}
