import { Badge, Group, Paper, Stack, Text, Title } from '@mantine/core'
import { useMyAssignments } from '@/features/assignments/assignmentsApi'
import { AssignmentStatusBadge } from '@/features/assignments/AssignmentTable'
import { formatDate, formatDay } from '@/lib/dates'

/** Everyone's own open assignments, earliest due date first. */
export function MyAssignmentsPage() {
  const { data, isPending } = useMyAssignments()

  return (
    <Stack maw={720}>
      <Title order={2}>Zimmetlerim</Title>
      {isPending ? (
        <Text c="dimmed">Yükleniyor…</Text>
      ) : !data || data.length === 0 ? (
        <Text c="dimmed">Üzerinizde zimmetli bir şey yok.</Text>
      ) : (
        data.map((a) => (
          <Paper key={a.id} withBorder p="md">
            <Group justify="space-between" align="flex-start" wrap="nowrap">
              <div>
                <Text fw={600}>{a.productName}</Text>
                <Text size="sm" ff="monospace">
                  {a.itemCode}
                  {a.serialNumber ? ` · ${a.serialNumber}` : ''}
                </Text>
                <Text size="xs" c="dimmed">
                  {formatDate(a.assignedAt)} tarihinde {a.assignedByName} verdi
                </Text>
                {a.note && (
                  <Text size="xs" c="dimmed">
                    Not: {a.note}
                  </Text>
                )}
              </div>
              <Stack gap={4} align="flex-end">
                <AssignmentStatusBadge status={a.status} />
                {a.dueDate ? (
                  <Badge variant="outline" color={a.status === 'Overdue' ? 'red' : 'gray'}>
                    Teslim: {formatDay(a.dueDate)}
                  </Badge>
                ) : (
                  <Text size="xs" c="dimmed">
                    Teslim tarihi yok
                  </Text>
                )}
              </Stack>
            </Group>
          </Paper>
        ))
      )}
    </Stack>
  )
}
