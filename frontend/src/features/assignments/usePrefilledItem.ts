import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'
import { getErrorMessage } from '@/api/client'
import type { ItemDetail } from '@/api/types'
import { fetchItem } from '@/features/assignments/assignmentsApi'

/**
 * Loads the item named by `?itemId=` (links from the item detail page and lists) and checks it
 * with the flow's rule. Returns the problem message when it cannot be used.
 */
export function usePrefilledItem(
  validate: (item: ItemDetail) => string | null,
  onItem: (item: ItemDetail) => void,
) {
  const [params] = useSearchParams()
  const itemId = params.get('itemId')
  const [problem, setProblem] = useState<string | null>(null)

  useEffect(() => {
    if (!itemId) return
    let cancelled = false
    fetchItem(itemId)
      .then((item) => {
        if (cancelled) return
        const message = validate(item)
        if (message) setProblem(message)
        else onItem(item)
      })
      .catch((e: unknown) => !cancelled && setProblem(getErrorMessage(e)))
    return () => {
      cancelled = true
    }
    // Runs once per item id; the callbacks are stable for the page's lifetime.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [itemId])

  return problem
}
