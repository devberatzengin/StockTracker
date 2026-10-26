import { Badge, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { useState, type ReactNode } from 'react'
import { useParams } from 'react-router'
import { isApiError } from '@/api/client'
import { NotFoundPage } from '@/components/NotFoundPage'
import { useUser } from '@/features/assignments/assignmentsApi'
import { AssignmentTable } from '@/features/assignments/AssignmentTable'
import { roleLabel } from '@/features/users/roles'

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" fw={500}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {children}
      </Text>
    </div>
  )
}

/** User info and assignment history (open ones first by date). */
export function UserDetailPage() {
  const { id = '' } = useParams()
  const { data: user, isPending, error } = useUser(id)
  const [page, setPage] = useState(1)

  if (isApiError(error, 404) || isApiError(error, 400)) return <NotFoundPage />
  if (isPending || !user) return <Text c="dimmed">Yükleniyor…</Text>

  return (
    <Stack maw={960}>
      <Group gap="xs">
        <Title order={2}>{user.fullName}</Title>
        {!user.isActive && (
          <Badge color="gray" variant="light">
            Pasif
          </Badge>
        )}
      </Group>

      <Paper withBorder p="md">
        <SimpleGrid cols={{ base: 1, xs: 2, md: 4 }}>
          <Field label="E-posta">{user.email ?? '—'}</Field>
          <Field label="Departman">{user.department ?? '—'}</Field>
          <Field label="Rol">{roleLabel(user.role)}</Field>
          <Field label="Giriş">{user.canLogin ? 'Giriş yapabilir' : 'Giriş yok'}</Field>
        </SimpleGrid>
      </Paper>

      <div>
        <Title order={4} mb="sm">
          Zimmet geçmişi
        </Title>
        <AssignmentTable
          filters={{ userId: user.id }}
          page={page}
          onPageChange={setPage}
          hide="assignee"
          canReturn
        />
      </div>
    </Stack>
  )
}
