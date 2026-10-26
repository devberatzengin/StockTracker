import { Anchor, Badge, Button, Group, Pagination, Stack, Table, Text } from '@mantine/core'
import { Link } from 'react-router'
import type { Assignment, AssignmentStatus } from '@/api/types'
import { useAssignments, type AssignmentFilters } from '@/features/assignments/assignmentsApi'
import {
  ASSIGNMENT_STATUS_COLORS,
  ASSIGNMENT_STATUS_LABELS,
  RETURN_CONDITION_LABELS,
} from '@/features/assignments/labels'
import { formatDate, formatDay } from '@/lib/dates'

export function AssignmentStatusBadge({ status }: { status: AssignmentStatus }) {
  return (
    <Badge variant="light" color={ASSIGNMENT_STATUS_COLORS[status]}>
      {ASSIGNMENT_STATUS_LABELS[status]}
    </Badge>
  )
}

interface Props {
  filters: Omit<AssignmentFilters, 'page'>
  page: number
  onPageChange: (page: number) => void
  /** Column to leave out when the page already shows it (the item's or the user's own page). */
  hide?: 'item' | 'assignee'
  /** Shows the "İade al" shortcut on open rows. */
  canReturn?: boolean
  emptyText?: string
}

/** Paged assignment list; used by the assignments page and the item/user history sections. */
export function AssignmentTable({
  filters,
  page,
  onPageChange,
  hide,
  canReturn = false,
  emptyText = 'Zimmet kaydı yok.',
}: Props) {
  const { data, isPending } = useAssignments({ ...filters, page })
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  if (isPending) return <Text c="dimmed">Yükleniyor…</Text>
  if (!data || data.items.length === 0) return <Text c="dimmed">{emptyText}</Text>

  return (
    <Stack>
      <Table.ScrollContainer minWidth={720}>
        <Table striped verticalSpacing="xs">
          <Table.Thead>
            <Table.Tr>
              {hide !== 'item' && <Table.Th>Adet</Table.Th>}
              {hide !== 'assignee' && <Table.Th>Kişi</Table.Th>}
              <Table.Th>Veriliş</Table.Th>
              <Table.Th>Teslim tarihi</Table.Th>
              <Table.Th>İade</Table.Th>
              <Table.Th>Durum</Table.Th>
              {canReturn && <Table.Th />}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.items.map((a) => (
              <AssignmentRow key={a.id} assignment={a} hide={hide} canReturn={canReturn} />
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {totalPages > 1 && (
        <Pagination value={page} onChange={onPageChange} total={totalPages} size="sm" />
      )}
    </Stack>
  )
}

function AssignmentRow({
  assignment: a,
  hide,
  canReturn,
}: {
  assignment: Assignment
  hide?: 'item' | 'assignee'
  canReturn: boolean
}) {
  return (
    <Table.Tr>
      {hide !== 'item' && (
        <Table.Td>
          <Anchor component={Link} to={`/items/${a.itemId}`} ff="monospace" size="sm">
            {a.itemCode}
          </Anchor>
          <Text size="xs" c="dimmed">
            {a.productName}
            {a.serialNumber ? ` · ${a.serialNumber}` : ''}
          </Text>
        </Table.Td>
      )}
      {hide !== 'assignee' && <Table.Td>{a.assigneeName}</Table.Td>}
      <Table.Td>
        <Text size="sm">{formatDate(a.assignedAt)}</Text>
        <Text size="xs" c="dimmed">
          {a.assignedByName}
        </Text>
        {a.note && (
          <Text size="xs" c="dimmed">
            Not: {a.note}
          </Text>
        )}
      </Table.Td>
      <Table.Td>{a.dueDate ? formatDay(a.dueDate) : '—'}</Table.Td>
      <Table.Td>
        {a.returnedAt ? (
          <>
            <Text size="sm">
              {formatDate(a.returnedAt)}
              {a.returnCondition ? ` · ${RETURN_CONDITION_LABELS[a.returnCondition]}` : ''}
            </Text>
            {a.returnLocationPath && (
              <Text size="xs" c="dimmed">
                {a.returnLocationPath}
              </Text>
            )}
            {a.returnNote && (
              <Text size="xs" c="dimmed">
                Not: {a.returnNote}
              </Text>
            )}
          </>
        ) : (
          '—'
        )}
      </Table.Td>
      <Table.Td>
        <AssignmentStatusBadge status={a.status} />
      </Table.Td>
      {canReturn && (
        <Table.Td>
          {a.status !== 'Returned' && (
            <Group justify="flex-end">
              <Button
                size="compact-sm"
                variant="light"
                component={Link}
                to={`/assignments/return?itemId=${a.itemId}`}
              >
                İade al
              </Button>
            </Group>
          )}
        </Table.Td>
      )}
    </Table.Tr>
  )
}
