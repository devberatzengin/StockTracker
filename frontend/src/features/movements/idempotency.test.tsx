import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useStockIn } from '@/features/movements/movementsApi'
import { json, Providers } from '@/test/utils'

const wrapper = ({ children }: { children: ReactNode }) => <Providers>{children}</Providers>
const body = { toLocationId: 'l-1', lines: [{ productId: 'p-1', quantity: 5, serialNumbers: null }] }

describe('stock mutations and Idempotency-Key', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('reuses the key when the same submission is retried and renews it after a success', async () => {
    const keys: (string | null)[] = []
    let fail = true
    vi.stubGlobal('fetch', async (_input: RequestInfo | URL, init?: RequestInit) => {
      keys.push(new Headers(init?.headers).get('Idempotency-Key'))
      if (fail) {
        fail = false
        throw new TypeError('network down')
      }
      return json(200, { referenceId: 'r-1', movementCount: 1, createdItems: [] })
    })
    const { result } = renderHook(() => useStockIn(), { wrapper })

    await expect(result.current.mutateAsync(body)).rejects.toBeTruthy()
    await result.current.mutateAsync(body)
    await result.current.mutateAsync(body)

    expect(keys).toHaveLength(3)
    expect(keys[0]).toBeTruthy()
    expect(keys[1]).toBe(keys[0])
    expect(keys[2]).not.toBe(keys[1])
  })
})
