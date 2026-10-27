import { BarChart, DonutChart } from '@mantine/charts'
import {
  Anchor,
  Badge,
  Group,
  Paper,
  SimpleGrid,
  Stack,
  Table,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core'
import {
  IconAlertTriangle,
  IconClockExclamation,
  IconPackage,
  IconStack2,
  IconTool,
  IconUserShare,
} from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { apiClient } from '@/api/client'
import type { Dashboard, MovementType } from '@/api/types'
import { formatQuantity, UNIT_LABELS } from '@/features/catalog/labels'
import {
  ITEM_STATUS_LABELS,
  MOVEMENT_TYPE_COLORS,
  MOVEMENT_TYPE_LABELS,
} from '@/features/movements/labels'
import { formatDay } from '@/lib/dates'

// Field of a daily row → movement type (the API sends one count per type).
const DAILY_SERIES: { key: string; type: MovementType }[] = [
  { key: 'in', type: 'In' },
  { key: 'out', type: 'Out' },
  { key: 'transfer', type: 'Transfer' },
  { key: 'adjustment', type: 'Adjustment' },
  { key: 'assign', type: 'Assign' },
  { key: 'return', type: 'Return' },
  { key: 'statusChange', type: 'StatusChange' },
]

function Card({
  label,
  value,
  icon,
  color,
  to,
}: {
  label: string
  value: number
  icon: ReactNode
  color: string
  to?: string
}) {
  const body = (
    <Paper withBorder p="md" radius="md">
      <Group justify="space-between" wrap="nowrap">
        <div>
          <Text size="xs" c="dimmed" fw={500}>
            {label}
          </Text>
          <Text fw={700} fz={28} lh={1.2}>
            {value.toLocaleString('tr-TR')}
          </Text>
        </div>
        <ThemeIcon size={40} radius="md" variant="light" color={color}>
          {icon}
        </ThemeIcon>
      </Group>
    </Paper>
  )
  return to ? (
    <Anchor component={Link} to={to} underline="never" c="inherit">
      {body}
    </Anchor>
  ) : (
    body
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Paper withBorder p="md" radius="md">
      <Title order={5} mb="sm">
        {title}
      </Title>
      {children}
    </Paper>
  )
}

export function DashboardPage() {
  const { data, isPending } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => apiClient.get<Dashboard>('/dashboard'),
  })

  if (isPending || !data) return <Text c="dimmed">Yükleniyor…</Text>

  const { cards } = data
  // Only movement types that occurred in the period get a bar series.
  const series = DAILY_SERIES.filter((s) =>
    data.dailyMovements.some((d) => (d as unknown as Record<string, number>)[s.key] > 0),
  ).map((s) => ({
    name: s.key,
    label: MOVEMENT_TYPE_LABELS[s.type],
    color: `${MOVEMENT_TYPE_COLORS[s.type]}.6`,
  }))
  const daily = data.dailyMovements.map((d) => ({
    ...d,
    day: formatDay(d.date).replace(/ \d{4}$/, ''),
  }))
  const statuses = [
    { name: ITEM_STATUS_LABELS.InStock, value: data.itemStatuses.inStock, color: 'green.6' },
    { name: ITEM_STATUS_LABELS.Assigned, value: data.itemStatuses.assigned, color: 'grape.6' },
    { name: ITEM_STATUS_LABELS.InRepair, value: data.itemStatuses.inRepair, color: 'orange.6' },
  ].filter((s) => s.value > 0)

  return (
    <Stack>
      <Title order={2}>Panel</Title>

      <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }}>
        <Card
          label="Aktif ürün"
          value={cards.activeProducts}
          icon={<IconPackage size={22} />}
          color="blue"
          to="/products"
        />
        <Card
          label="Toplam adet"
          value={cards.totalItems}
          icon={<IconStack2 size={22} />}
          color="gray"
        />
        <Card
          label="Kritik stok"
          value={cards.lowStockProducts}
          icon={<IconAlertTriangle size={22} />}
          color="orange"
        />
        <Card
          label="Açık zimmet"
          value={cards.openAssignments}
          icon={<IconUserShare size={22} />}
          color="grape"
          to="/assignments"
        />
        <Card
          label="Gecikmiş zimmet"
          value={cards.overdueAssignments}
          icon={<IconClockExclamation size={22} />}
          color="red"
          to="/assignments"
        />
        <Card
          label="Tamirdeki adet"
          value={cards.itemsInRepair}
          icon={<IconTool size={22} />}
          color="yellow"
        />
      </SimpleGrid>

      <Section title="Son 30 gün · günlük hareketler">
        {series.length === 0 ? (
          <Text c="dimmed" size="sm">
            Son 30 günde hareket yok.
          </Text>
        ) : (
          <BarChart
            h={260}
            data={daily}
            dataKey="day"
            type="stacked"
            series={series}
            withLegend
            tickLine="y"
          />
        )}
      </Section>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Section title="Son 30 günde en çok hareket gören ürünler">
          {data.topProducts.length === 0 ? (
            <Text c="dimmed" size="sm">
              Hareket yok.
            </Text>
          ) : (
            <BarChart
              h={Math.max(160, data.topProducts.length * 32)}
              data={data.topProducts.map((p) => ({ name: p.name, movementCount: p.movementCount }))}
              dataKey="name"
              orientation="vertical"
              yAxisProps={{ width: 120 }}
              series={[{ name: 'movementCount', label: 'Hareket', color: 'blue.6' }]}
            />
          )}
        </Section>
        <Section title="Adet durum dağılımı">
          {statuses.length === 0 ? (
            <Text c="dimmed" size="sm">
              Adet yok.
            </Text>
          ) : (
            <Group justify="center">
              <DonutChart
                data={statuses}
                withLabelsLine
                withLabels
                withTooltip
                size={180}
                thickness={28}
              />
              <Stack gap={4}>
                {statuses.map((s) => (
                  <Group key={s.name} gap={6}>
                    <Badge color={s.color.split('.')[0]} variant="filled" size="xs" circle>
                      {' '}
                    </Badge>
                    <Text size="sm">
                      {s.name}: {s.value}
                    </Text>
                  </Group>
                ))}
              </Stack>
            </Group>
          )}
        </Section>
      </SimpleGrid>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Section title="Kritik stoktaki ürünler">
          {data.lowStock.length === 0 ? (
            <Text c="dimmed" size="sm">
              Kritik stokta ürün yok.
            </Text>
          ) : (
            <Table verticalSpacing={4} fz="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Ürün</Table.Th>
                  <Table.Th ta="right">Stok</Table.Th>
                  <Table.Th ta="right">Minimum</Table.Th>
                  <Table.Th ta="right">Eksik</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.lowStock.map((p) => {
                  const unit = UNIT_LABELS[p.unit].toLocaleLowerCase('tr-TR')
                  return (
                    <Table.Tr key={p.productId}>
                      <Table.Td>
                        <Anchor component={Link} to={`/products/${p.productId}`} size="sm">
                          {p.name}
                        </Anchor>
                      </Table.Td>
                      <Table.Td ta="right">{formatQuantity(p.total)}</Table.Td>
                      <Table.Td ta="right">{formatQuantity(p.minStockLevel)}</Table.Td>
                      <Table.Td ta="right" c="red" fw={600}>
                        {formatQuantity(p.shortage)} {unit}
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          )}
        </Section>
        <Section title="En eski gecikmiş zimmetler">
          {data.overdueAssignments.length === 0 ? (
            <Text c="dimmed" size="sm">
              Gecikmiş zimmet yok.
            </Text>
          ) : (
            <Table verticalSpacing={4} fz="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Adet</Table.Th>
                  <Table.Th>Kişi</Table.Th>
                  <Table.Th>Teslim</Table.Th>
                  <Table.Th ta="right">Gecikme</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.overdueAssignments.map((a) => (
                  <Table.Tr key={a.assignmentId}>
                    <Table.Td>
                      <Anchor component={Link} to={`/items/${a.itemId}`} size="sm" ff="monospace">
                        {a.itemCode}
                      </Anchor>
                      <Text size="xs" c="dimmed">
                        {a.productName}
                      </Text>
                    </Table.Td>
                    <Table.Td>{a.assigneeName}</Table.Td>
                    <Table.Td>{formatDay(a.dueDate)}</Table.Td>
                    <Table.Td ta="right" c="red" fw={600}>
                      {a.daysOverdue} gün
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Section>
      </SimpleGrid>
    </Stack>
  )
}
