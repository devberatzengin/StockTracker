import { Badge, Group, Pagination, Paper, Stack, Table, Text } from '@mantine/core'
import { useState } from 'react'
import type { AuditLog } from '@/api/types'
import { useAuditLogs, type AuditFilters } from '@/features/audit/auditApi'
import {
  AUDIT_ACTION_COLORS,
  AUDIT_ACTION_LABELS,
  ENTITY_TYPE_LABELS,
  fieldLabel,
  formatAuditValue,
  isVisibleField,
} from '@/features/audit/auditLabels'
import { formatDateTime } from '@/lib/dates'

interface Props {
  filters: Omit<AuditFilters, 'page'>
  /** Shows which record each row is about (the admin page lists many records). */
  showEntity?: boolean
}

/** "Değişiklik geçmişi": who changed what, old → new, newest first. */
export function AuditHistory({ filters, showEntity = false }: Props) {
  const [page, setPage] = useState(1)
  const { data, isPending } = useAuditLogs({ ...filters, page })
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  if (isPending) return <Text c="dimmed">Yükleniyor…</Text>
  if (!data || data.items.length === 0) return <Text c="dimmed">Değişiklik kaydı yok.</Text>

  return (
    <Stack gap="xs">
      {data.items.map((row) => (
        <AuditRow
          key={row.id}
          row={row}
          showEntity={showEntity || row.entityType !== filters.entityType}
        />
      ))}
      {totalPages > 1 && (
        <Pagination value={page} onChange={setPage} total={totalPages} size="sm" />
      )}
    </Stack>
  )
}

function AuditRow({ row, showEntity }: { row: AuditLog; showEntity: boolean }) {
  const changes = row.changes.filter((c) => isVisibleField(c.field))
  return (
    <Paper withBorder p="sm">
      <Group justify="space-between" mb={changes.length ? 6 : 0}>
        <Group gap="xs">
          <Badge variant="light" color={AUDIT_ACTION_COLORS[row.action]}>
            {AUDIT_ACTION_LABELS[row.action]}
          </Badge>
          {showEntity && (
            <Text size="sm" fw={500}>
              {ENTITY_TYPE_LABELS[row.entityType] ?? row.entityType}
              {row.entityLabel ? `: ${row.entityLabel}` : ''}
            </Text>
          )}
        </Group>
        <Text size="xs" c="dimmed">
          {row.userName ?? 'Sistem'} · {formatDateTime(row.createdAt)}
        </Text>
      </Group>
      {changes.length > 0 && (
        <Table withRowBorders={false} verticalSpacing={2} fz="sm">
          <Table.Tbody>
            {changes.map((c) => (
              <Table.Tr key={c.field}>
                <Table.Td w={160} c="dimmed">
                  {fieldLabel(c.field)}
                </Table.Td>
                <Table.Td>
                  {row.action === 'Created' ? (
                    formatAuditValue(c.field, c.new)
                  ) : row.action === 'Deleted' ? (
                    <Text span td="line-through" size="sm">
                      {formatAuditValue(c.field, c.old)}
                    </Text>
                  ) : (
                    <>
                      <Text span c="dimmed" size="sm">
                        {formatAuditValue(c.field, c.old)}
                      </Text>
                      {' → '}
                      <Text span fw={500} size="sm">
                        {formatAuditValue(c.field, c.new)}
                      </Text>
                    </>
                  )}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Paper>
  )
}
