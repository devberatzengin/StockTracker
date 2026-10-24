import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Group, Modal, Select, Stack, Text, TextInput } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { applyServerErrors } from '@/api/formErrors'
import type { LocationType } from '@/api/types'
import { LOCATION_TYPE_OPTIONS } from '@/features/locations/labels'
import { useCreateLocation, useUpdateLocation } from '@/features/locations/locationsApi'

const schema = z.object({
  name: z.string().trim().min(1, 'Lokasyon adı zorunludur.').max(100, 'En fazla 100 karakter.'),
  type: z.enum(['Building', 'Room', 'Cabinet', 'Shelf', 'Box', 'Other']),
})
type FormValues = z.infer<typeof schema>

export type LocationFormTarget =
  | { mode: 'create'; parent: { id: string; pathLabel: string } | null }
  | { mode: 'edit'; location: { id: string; name: string; type: LocationType } }

interface Props {
  target: LocationFormTarget | null
  onClose: () => void
  onSaved?: (id: string) => void
}

export function LocationFormModal({ target, onClose, onSaved }: Props) {
  const title = target?.mode === 'edit' ? 'Lokasyonu düzenle' : 'Yeni lokasyon'
  return (
    <Modal opened={!!target} onClose={onClose} title={title}>
      {/* Modal content unmounts on close, so every open starts with fresh form state. */}
      {target && <LocationForm target={target} onClose={onClose} onSaved={onSaved} />}
    </Modal>
  )
}

function defaultType(target: LocationFormTarget): LocationType {
  if (target.mode === 'edit') return target.location.type
  return target.parent ? 'Room' : 'Building'
}

function LocationForm({
  target,
  onClose,
  onSaved,
}: {
  target: LocationFormTarget
  onClose: () => void
  onSaved?: (id: string) => void
}) {
  const createLocation = useCreateLocation()
  const updateLocation = useUpdateLocation()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: target.mode === 'edit' ? target.location.name : '',
      type: defaultType(target),
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      if (target.mode === 'edit') {
        await updateLocation.mutateAsync({ id: target.location.id, ...values })
        notifications.show({ color: 'green', message: 'Lokasyon güncellendi.' })
        onSaved?.(target.location.id)
      } else {
        const { id } = await createLocation.mutateAsync({
          ...values,
          parentId: target.parent?.id ?? null,
        })
        notifications.show({ color: 'green', message: 'Lokasyon eklendi.' })
        onSaved?.(id)
      }
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['name', 'type']))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        {target.mode === 'create' && (
          <Text size="sm" c="dimmed">
            Üst lokasyon: <b>{target.parent?.pathLabel ?? 'Yok (kök lokasyon)'}</b>
          </Text>
        )}
        {formError && <Alert color="red">{formError}</Alert>}
        <TextInput
          label="Ad"
          required
          data-autofocus
          error={errors.name?.message}
          {...register('name')}
        />
        <Controller
          control={control}
          name="type"
          render={({ field }) => (
            <Select
              label="Tip"
              required
              data={LOCATION_TYPE_OPTIONS}
              allowDeselect={false}
              value={field.value}
              onChange={(value) => value && field.onChange(value as LocationType)}
              error={errors.type?.message}
            />
          )}
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Kaydet
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
