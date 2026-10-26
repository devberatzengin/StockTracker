import { Button, Group, SegmentedControl, Stack, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { IconArrowBackUp, IconUserShare } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { AssignmentStatus, UserLookup } from '@/api/types'
import { AssignmentTable } from '@/features/assignments/AssignmentTable'
import { ASSIGNMENT_STATUS_LABELS } from '@/features/assignments/labels'
import { UserPicker } from '@/features/assignments/UserPicker'

type StatusFilter = AssignmentStatus | 'all'

const STATUS_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'Open', label: ASSIGNMENT_STATUS_LABELS.Open },
  { value: 'Overdue', label: ASSIGNMENT_STATUS_LABELS.Overdue },
  { value: 'Returned', label: ASSIGNMENT_STATUS_LABELS.Returned },
  { value: 'all', label: 'Tümü' },
]

/** Warehouse view: open, overdue and returned assignments with person and item search. */
export function AssignmentsPage() {
  const [status, setStatus] = useState<StatusFilter>('Open')
  const [user, setUser] = useState<UserLookup | null>(null)
  const [search, setSearch] = useState('')
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300)
  const [page, setPage] = useState(1)

  const filters = {
    status: status === 'all' ? null : status,
    userId: user?.id,
    search: debouncedSearch,
    sort: status === 'Returned' ? '-returnedAt' : undefined,
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Zimmetler</Title>
        <Group gap="xs">
          <Button component={Link} to="/assignments/new" leftSection={<IconUserShare size={16} />}>
            Zimmet ver
          </Button>
          <Button
            component={Link}
            to="/assignments/return"
            variant="light"
            leftSection={<IconArrowBackUp size={16} />}
          >
            İade al
          </Button>
        </Group>
      </Group>

      <Group align="flex-end" gap="sm">
        <SegmentedControl
          value={status}
          onChange={(value) => {
            setStatus(value as StatusFilter)
            setPage(1)
          }}
          data={STATUS_OPTIONS}
        />
        <UserPicker
          label="Kişi"
          value={user}
          onChange={(u) => {
            setUser(u)
            setPage(1)
          }}
        />
        <TextInput
          label="Adet / ürün"
          placeholder="Kod, seri no ya da ürün adı"
          value={search}
          onChange={(e) => {
            setSearch(e.currentTarget.value)
            setPage(1)
          }}
        />
      </Group>

      <AssignmentTable
        filters={filters}
        page={page}
        onPageChange={setPage}
        canReturn
        emptyText="Bu filtrelere uyan zimmet yok."
      />
    </Stack>
  )
}
