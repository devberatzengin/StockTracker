import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  FileButton,
  Group,
  Image,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  Tooltip,
} from '@mantine/core'
import { modals } from '@mantine/modals'
import {
  IconCamera,
  IconChevronLeft,
  IconChevronRight,
  IconStar,
  IconTrash,
} from '@tabler/icons-react'
import { useState } from 'react'
import type { ProductPhoto } from '@/api/types'
import { move } from '@/features/catalog/galleryOrder'
import { usePhotoActions, useUploadPhoto } from '@/features/catalog/photosApi'
import { parseLineErrors } from '@/features/movements/lineErrors'

export const MAX_PHOTOS = 10

interface Props {
  productId: string
  photos: ProductPhoto[]
  canManage: boolean
}

/**
 * Product photos: upload (the phone opens its camera), drag to reorder (arrows on touch screens),
 * make primary, delete. The first photo is the primary one until another is chosen.
 */
export function ProductGallery({ productId, photos, canManage }: Props) {
  const upload = useUploadPhoto(productId)
  const { setPrimary, reorder, remove } = usePhotoActions(productId)
  const [error, setError] = useState<string | null>(null)
  const [dragged, setDragged] = useState<number | null>(null)

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
    } catch (e) {
      const parsed = parseLineErrors(e)
      setError(
        parsed.fields.file ??
          Object.values(parsed.fields)[0] ??
          parsed.general[0] ??
          'İşlem yapılamadı.',
      )
    }
  }

  const uploadFiles = async (files: File[]) => {
    for (const file of files.slice(0, MAX_PHOTOS - photos.length)) {
      await run(() => upload.mutateAsync(file))
    }
  }

  const moveTo = (from: number, to: number) =>
    void run(() => reorder.mutateAsync(move(photos, from, to).map((p) => p.id)))

  const confirmDelete = (photo: ProductPhoto) =>
    modals.openConfirmModal({
      title: 'Fotoğrafı sil',
      children: (
        <Text size="sm">
          Fotoğraf kalıcı olarak silinir.
          {photo.isPrimary && photos.length > 1 ? ' Sıradaki fotoğraf ana fotoğraf olur.' : ''}
        </Text>
      ),
      labels: { confirm: 'Sil', cancel: 'Vazgeç' },
      confirmProps: { color: 'red' },
      onConfirm: () => void run(() => remove.mutateAsync(photo.id)),
    })

  return (
    <Stack gap="xs">
      <Group justify="space-between">
        <Text size="xs" c="dimmed" fw={500}>
          Fotoğraflar ({photos.length}/{MAX_PHOTOS})
        </Text>
        {canManage && (
          <FileButton
            onChange={(files) => void uploadFiles(files)}
            accept="image/*"
            capture="environment"
            multiple
            disabled={photos.length >= MAX_PHOTOS}
          >
            {(props) => (
              <Button
                {...props}
                size="xs"
                variant="light"
                leftSection={<IconCamera size={14} />}
                loading={upload.isPending}
              >
                Fotoğraf ekle
              </Button>
            )}
          </FileButton>
        )}
      </Group>

      {error && (
        <Alert color="red" withCloseButton onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {photos.length === 0 ? (
        <Text size="sm" c="dimmed">
          Fotoğraf yok.
        </Text>
      ) : (
        <SimpleGrid cols={{ base: 2, xs: 3, sm: 4, md: 5 }} spacing="xs">
          {photos.map((photo, index) => (
            <Paper
              key={photo.id}
              withBorder
              p={4}
              draggable={canManage}
              onDragStart={() => setDragged(index)}
              onDragOver={(e) => canManage && e.preventDefault()}
              onDrop={() => {
                if (dragged !== null) moveTo(dragged, index)
                setDragged(null)
              }}
              style={{
                cursor: canManage ? 'grab' : undefined,
                opacity: dragged === index ? 0.5 : 1,
              }}
              data-testid={`photo-${photo.id}`}
            >
              <a href={photo.url} target="_blank" rel="noreferrer">
                <Image
                  src={photo.thumbnailUrl}
                  alt={`Fotoğraf ${index + 1}`}
                  h={120}
                  fit="cover"
                  radius="sm"
                />
              </a>
              <Group justify="space-between" gap={2} mt={4} wrap="nowrap">
                {photo.isPrimary ? (
                  <Badge size="xs" leftSection={<IconStar size={10} />}>
                    Ana
                  </Badge>
                ) : canManage ? (
                  <Button
                    size="compact-xs"
                    variant="subtle"
                    onClick={() => void run(() => setPrimary.mutateAsync(photo.id))}
                  >
                    Ana yap
                  </Button>
                ) : (
                  <span />
                )}
                {canManage && (
                  <Group gap={0} wrap="nowrap">
                    <ActionIcon
                      size="sm"
                      variant="subtle"
                      aria-label={`Fotoğraf ${index + 1} sola taşı`}
                      disabled={index === 0}
                      onClick={() => moveTo(index, index - 1)}
                    >
                      <IconChevronLeft size={14} />
                    </ActionIcon>
                    <ActionIcon
                      size="sm"
                      variant="subtle"
                      aria-label={`Fotoğraf ${index + 1} sağa taşı`}
                      disabled={index === photos.length - 1}
                      onClick={() => moveTo(index, index + 1)}
                    >
                      <IconChevronRight size={14} />
                    </ActionIcon>
                    <Tooltip label="Sil">
                      <ActionIcon
                        size="sm"
                        variant="subtle"
                        color="red"
                        aria-label={`Fotoğraf ${index + 1} sil`}
                        onClick={() => confirmDelete(photo)}
                      >
                        <IconTrash size={14} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>
                )}
              </Group>
            </Paper>
          ))}
        </SimpleGrid>
      )}
    </Stack>
  )
}
