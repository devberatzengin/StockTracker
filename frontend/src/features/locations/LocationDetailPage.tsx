import {
  Alert,
  Anchor,
  Badge,
  Breadcrumbs,
  Button,
  Group,
  Stack,
  Table,
  Text,
  Title,
} from '@mantine/core'
import { IconArrowLeft } from '@tabler/icons-react'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/client'
import { NotFoundPage } from '@/components/NotFoundPage'
import { LOCATION_TYPE_LABELS } from '@/features/locations/labels'
import { LocationTypeIcon } from '@/features/locations/LocationTypeIcon'
import { useLocation } from '@/features/locations/locationsApi'

export function LocationDetailPage() {
  const { id = '' } = useParams()
  const { data: location, isPending, error } = useLocation(id)

  if (isApiError(error, 404) || isApiError(error, 400)) {
    return <NotFoundPage />
  }

  if (isPending || !location) {
    return <Text c="dimmed">Yükleniyor…</Text>
  }

  return (
    <Stack>
      <Button
        component={Link}
        to="/locations"
        variant="subtle"
        leftSection={<IconArrowLeft size={16} />}
        w="fit-content"
        px={0}
      >
        Lokasyonlar
      </Button>

      <Breadcrumbs separator="›">
        {location.ancestors.map((ancestor) => (
          <Anchor key={ancestor.id} component={Link} to={`/locations/${ancestor.id}`} size="sm">
            {ancestor.name}
          </Anchor>
        ))}
        <Text size="sm" fw={500}>
          {location.name}
        </Text>
      </Breadcrumbs>

      <div>
        <Group gap="xs">
          <LocationTypeIcon type={location.type} size={24} />
          <Title order={2}>{location.name}</Title>
          <Badge variant="light">{LOCATION_TYPE_LABELS[location.type]}</Badge>
          {!location.isActive && <Badge color="gray">Pasif</Badge>}
        </Group>
        <Text ff="monospace" c="dimmed">
          {location.code}
        </Text>
      </div>

      {!location.isActive && <Alert color="gray">Bu lokasyon pasif.</Alert>}

      <div>
        <Title order={4} mb="xs">
          Alt lokasyonlar
        </Title>
        {location.children.length === 0 ? (
          <Text size="sm" c="dimmed">
            Alt lokasyon yok.
          </Text>
        ) : (
          <Table striped withTableBorder maw={640}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Ad</Table.Th>
                <Table.Th>Tip</Table.Th>
                <Table.Th>Kod</Table.Th>
                <Table.Th>Durum</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {location.children.map((child) => (
                <Table.Tr key={child.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/locations/${child.id}`} size="sm">
                      {child.name}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>{LOCATION_TYPE_LABELS[child.type]}</Table.Td>
                  <Table.Td ff="monospace">{child.code}</Table.Td>
                  <Table.Td>
                    <Badge variant="dot" color={child.isActive ? 'green' : 'gray'}>
                      {child.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </div>
    </Stack>
  )
}
