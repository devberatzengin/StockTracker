import { Alert, Badge, Group, Paper, Stack, Text } from '@mantine/core'
import { useState } from 'react'
import { getErrorMessage, isApiError } from '@/api/client'
import type { ItemDetail } from '@/api/types'
import { fetchItem } from '@/features/assignments/assignmentsApi'
import { ItemStatusBadge } from '@/features/movements/ProductStockTabs'
import { lookupScan } from '@/features/scan/scanApi'
import { ScanPanel } from '@/features/scan/ScanDialog'
import { formatDay } from '@/lib/dates'

interface Props {
  /** Returns an error message when the item cannot be used in this flow. */
  validate: (item: ItemDetail) => string | null
  onItem: (item: ItemDetail) => void
}

/** Step 1 of the assign/return flows: scan (or type) the item's own label. */
export function ItemScanStep({ validate, onItem }: Props) {
  const [message, setMessage] = useState<string | null>(null)

  const onScan = async (input: string) => {
    setMessage(null)
    try {
      const result = await lookupScan(input)
      if (result.type !== 'item') {
        setMessage(`"${result.title}" bir adet değil. Adetin kendi etiketini (ITM-…) okutun.`)
        return
      }
      const item = await fetchItem(result.id)
      const problem = validate(item)
      if (problem) {
        setMessage(problem)
        return
      }
      onItem(item)
    } catch (e) {
      setMessage(isApiError(e, 404) ? 'Bu koda ait adet bulunamadı.' : getErrorMessage(e))
    }
  }

  return (
    <Stack>
      <Text size="sm">Adetin etiketini okutun ya da kodunu girin.</Text>
      {message && <Alert color="orange">{message}</Alert>}
      <ScanPanel onScan={onScan} mode="single" />
    </Stack>
  )
}

/** Compact item summary shown on later steps. */
export function ItemCard({ item }: { item: ItemDetail }) {
  return (
    <Paper withBorder p="sm" bg="var(--mantine-color-default-hover)">
      <Group justify="space-between" wrap="nowrap" align="flex-start">
        <div>
          <Text fw={600} ff="monospace">
            {item.code}
          </Text>
          <Text size="sm">
            {item.productName}
            {item.serialNumber ? ` · ${item.serialNumber}` : ''}
          </Text>
          {item.locationPath && (
            <Text size="xs" c="dimmed">
              {item.locationPath}
            </Text>
          )}
          {item.openAssignment && (
            <Text size="xs" c="dimmed">
              Zimmetli: {item.openAssignment.assigneeName}
              {item.openAssignment.dueDate
                ? ` · teslim ${formatDay(item.openAssignment.dueDate)}`
                : ''}
            </Text>
          )}
        </div>
        <Stack gap={4} align="flex-end">
          <ItemStatusBadge status={item.status} />
          {item.openAssignment?.isOverdue && (
            <Badge color="red" variant="light">
              Gecikmiş
            </Badge>
          )}
        </Stack>
      </Group>
    </Paper>
  )
}
