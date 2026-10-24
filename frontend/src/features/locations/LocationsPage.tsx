import {
  Badge,
  Button,
  Grid,
  Group,
  Paper,
  ScrollArea,
  Stack,
  Text,
  TextInput,
  Title,
  Tree,
  getTreeExpandedState,
  useTree,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import { notifications } from '@mantine/notifications'
import {
  IconArchive,
  IconArrowsMove,
  IconChevronDown,
  IconChevronRight,
  IconExternalLink,
  IconPencil,
  IconPlus,
  IconSearch,
} from '@tabler/icons-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import type { LocationType } from '@/api/types'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import { LOCATION_TYPE_LABELS } from '@/features/locations/labels'
import { LocationTypeIcon } from '@/features/locations/LocationTypeIcon'
import { LocationFormModal, type LocationFormTarget } from '@/features/locations/LocationFormModal'
import { useDeactivateLocation, useLocationTree } from '@/features/locations/locationsApi'
import { MoveLocationModal } from '@/features/locations/MoveLocationModal'
import {
  filterTree,
  flattenTree,
  toTreeData,
  type FlatLocation,
} from '@/features/locations/treeUtils'

export function LocationsPage() {
  const canManage = usePermission(Permissions.CanManageCatalog)
  const { data: tree = [], isPending } = useLocationTree()
  const deactivate = useDeactivateLocation()

  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [formTarget, setFormTarget] = useState<LocationFormTarget | null>(null)
  const [moving, setMoving] = useState<FlatLocation | null>(null)
  // null = never toggled: everything starts expanded.
  const [manualExpanded, setManualExpanded] = useState<Record<string, boolean> | null>(null)

  const visibleTree = useMemo(() => filterTree(tree, search), [tree, search])
  const treeData = useMemo(() => toTreeData(visibleTree), [visibleTree])
  const flatById = useMemo(() => new Map(flattenTree(tree).map((f) => [f.node.id, f])), [tree])
  const selected = selectedId ? (flatById.get(selectedId) ?? null) : null

  // While searching, show every match expanded; otherwise keep the user's expand/collapse choices.
  const expandedState =
    search.trim() || manualExpanded === null ? getTreeExpandedState(treeData, '*') : manualExpanded
  const treeController = useTree({ expandedState, onExpandedStateChange: setManualExpanded })

  const confirmDeactivate = (location: FlatLocation) =>
    modals.openConfirmModal({
      title: 'Lokasyonu pasifleştir',
      children: (
        <Text size="sm">
          <b>{location.pathLabel}</b> ve tüm alt lokasyonları pasifleştirilsin mi?
        </Text>
      ),
      labels: { confirm: 'Pasifleştir', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        deactivate.mutate(location.node.id, {
          onSuccess: () => {
            setSelectedId(null)
            notifications.show({ color: 'green', message: 'Lokasyon pasifleştirildi.' })
          },
        }),
    })

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Lokasyonlar</Title>
        {canManage && (
          <Button
            leftSection={<IconPlus size={18} />}
            onClick={() => setFormTarget({ mode: 'create', parent: null })}
          >
            Yeni kök lokasyon
          </Button>
        )}
      </Group>

      <Grid>
        <Grid.Col span={{ base: 12, md: 7 }}>
          <Paper withBorder p="sm">
            <Stack gap="sm">
              <TextInput
                placeholder="Ad veya kod ara"
                leftSection={<IconSearch size={16} />}
                value={search}
                onChange={(event) => setSearch(event.currentTarget.value)}
              />
              <ScrollArea.Autosize mah={560}>
                {isPending ? (
                  <Text size="sm" c="dimmed">
                    Yükleniyor…
                  </Text>
                ) : treeData.length === 0 ? (
                  <Text size="sm" c="dimmed">
                    {search ? 'Eşleşen lokasyon yok.' : 'Henüz lokasyon yok.'}
                  </Text>
                ) : (
                  <Tree
                    data={treeData}
                    tree={treeController}
                    levelOffset="md"
                    aria-label="Lokasyon ağacı"
                    renderNode={({ node, expanded, hasChildren, elementProps }) => {
                      const { code, type } = node.nodeProps as { code: string; type: LocationType }
                      const isSelected = node.value === selectedId
                      return (
                        <Group
                          {...elementProps}
                          gap={6}
                          wrap="nowrap"
                          py={4}
                          px={6}
                          style={{
                            ...elementProps.style,
                            borderRadius: 'var(--mantine-radius-sm)',
                            background: isSelected ? 'var(--mantine-color-blue-light)' : undefined,
                          }}
                          onClick={(event) => {
                            elementProps.onClick(event)
                            setSelectedId(node.value)
                          }}
                        >
                          {hasChildren ? (
                            expanded ? (
                              <IconChevronDown size={14} />
                            ) : (
                              <IconChevronRight size={14} />
                            )
                          ) : (
                            <span style={{ width: 14 }} />
                          )}
                          <LocationTypeIcon type={type} />
                          <Text size="sm" fw={isSelected ? 600 : 400} truncate>
                            {node.label}
                          </Text>
                          <Text size="xs" c="dimmed" ff="monospace">
                            {code}
                          </Text>
                        </Group>
                      )
                    }}
                  />
                )}
              </ScrollArea.Autosize>
            </Stack>
          </Paper>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 5 }}>
          <Paper withBorder p="md">
            {selected ? (
              <Stack gap="sm">
                <Group gap="xs">
                  <LocationTypeIcon type={selected.node.type} size={20} />
                  <Title order={4}>{selected.node.name}</Title>
                </Group>
                <Group gap="xs">
                  <Badge variant="light">{LOCATION_TYPE_LABELS[selected.node.type]}</Badge>
                  <Text size="sm" ff="monospace" c="dimmed">
                    {selected.node.code}
                  </Text>
                </Group>
                <Text size="sm" c="dimmed">
                  {selected.pathLabel}
                </Text>
                <Text size="sm">Alt lokasyon sayısı: {selected.node.children.length}</Text>

                <Stack gap="xs" mt="xs">
                  <Button
                    component={Link}
                    to={`/locations/${selected.node.id}`}
                    variant="light"
                    leftSection={<IconExternalLink size={16} />}
                  >
                    Detay
                  </Button>
                  {canManage && (
                    <>
                      <Button
                        variant="light"
                        leftSection={<IconPlus size={16} />}
                        onClick={() =>
                          setFormTarget({
                            mode: 'create',
                            parent: { id: selected.node.id, pathLabel: selected.pathLabel },
                          })
                        }
                      >
                        Alt lokasyon ekle
                      </Button>
                      <Button
                        variant="light"
                        leftSection={<IconPencil size={16} />}
                        onClick={() => setFormTarget({ mode: 'edit', location: selected.node })}
                      >
                        Yeniden adlandır
                      </Button>
                      <Button
                        variant="light"
                        leftSection={<IconArrowsMove size={16} />}
                        onClick={() => setMoving(selected)}
                      >
                        Taşı
                      </Button>
                      <Button
                        variant="light"
                        color="red"
                        leftSection={<IconArchive size={16} />}
                        onClick={() => confirmDeactivate(selected)}
                      >
                        Pasifleştir
                      </Button>
                    </>
                  )}
                </Stack>
              </Stack>
            ) : (
              <Text size="sm" c="dimmed">
                Ayrıntıları görmek için ağaçtan bir lokasyon seçin.
              </Text>
            )}
          </Paper>
        </Grid.Col>
      </Grid>

      <LocationFormModal
        target={formTarget}
        onClose={() => setFormTarget(null)}
        onSaved={setSelectedId}
      />
      <MoveLocationModal location={moving} tree={tree} onClose={() => setMoving(null)} />
    </Stack>
  )
}
