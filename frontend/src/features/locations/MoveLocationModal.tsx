import { Alert, Button, Group, Modal, Select, Stack, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMemo, useState } from 'react'
import { getErrorMessage } from '@/api/client'
import type { LocationTreeNode } from '@/api/types'
import { useMoveLocation } from '@/features/locations/locationsApi'
import { moveTargetOptions, ROOT_VALUE, type FlatLocation } from '@/features/locations/treeUtils'

interface Props {
  /** The location being moved; null closes the modal. */
  location: FlatLocation | null
  tree: LocationTreeNode[]
  onClose: () => void
}

export function MoveLocationModal({ location, tree, onClose }: Props) {
  return (
    <Modal opened={!!location} onClose={onClose} title="Lokasyonu taşı">
      {location && <MoveForm location={location} tree={tree} onClose={onClose} />}
    </Modal>
  )
}

function MoveForm({
  location,
  tree,
  onClose,
}: {
  location: FlatLocation
  tree: LocationTreeNode[]
  onClose: () => void
}) {
  const move = useMoveLocation()
  const [target, setTarget] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const options = useMemo(() => moveTargetOptions(location, tree), [location, tree])

  const submit = async () => {
    if (!target) {
      setError('Hedef lokasyon seçin.')
      return
    }
    setError(null)
    try {
      await move.mutateAsync({
        id: location.node.id,
        newParentId: target === ROOT_VALUE ? null : target,
      })
      notifications.show({ color: 'green', message: 'Lokasyon taşındı.' })
      onClose()
    } catch (e) {
      setError(getErrorMessage(e))
    }
  }

  return (
    <Stack>
      <Text size="sm">
        <b>{location.pathLabel}</b> alt lokasyonlarıyla birlikte taşınacak.
      </Text>
      {error && <Alert color="red">{error}</Alert>}
      <Select
        label="Hedef üst lokasyon"
        placeholder="Lokasyon ara"
        data={options}
        value={target}
        onChange={setTarget}
        searchable
        nothingFoundMessage="Lokasyon bulunamadı"
        description="Lokasyonun kendisi, alt lokasyonları ve şu anki üstü seçilemez."
      />
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Vazgeç
        </Button>
        <Button onClick={() => void submit()} loading={move.isPending}>
          Taşı
        </Button>
      </Group>
    </Stack>
  )
}
