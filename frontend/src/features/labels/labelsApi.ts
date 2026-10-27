import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/api/client'
import type { LabelTemplate } from '@/api/types'
import type { LabelTarget } from '@/features/labels/labelTemplates'

/**
 * Opens the PDF in a new tab for printing. The tab is opened before the request (still inside the
 * click) so popup blockers allow it; without a tab the file is downloaded instead.
 */
export function usePrintLabels() {
  return useMutation({
    mutationFn: async ({
      template,
      startPosition,
      target,
      tab,
    }: {
      template: LabelTemplate
      startPosition: number
      target: LabelTarget
      tab: Window | null
    }) => {
      const pdf = await apiClient.postForBlob('/labels', { template, startPosition, ...target })
      const url = URL.createObjectURL(pdf)
      if (tab) {
        tab.location.href = url
      } else {
        const link = document.createElement('a')
        link.href = url
        link.download = 'etiketler.pdf'
        link.click()
      }
      // Give the tab time to load before releasing the object URL.
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    },
    meta: { skipGlobalError: true },
  })
}
