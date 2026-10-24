import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Group, Modal, Stack, TextInput } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { applyServerErrors } from '@/api/formErrors'
import type { CategoryDto } from '@/api/types'
import { useSaveCategory } from '@/features/catalog/catalogApi'

const schema = z.object({
  name: z.string().trim().min(1, 'Kategori adı zorunludur.').max(100, 'En fazla 100 karakter.'),
})
type FormValues = z.infer<typeof schema>

interface Props {
  opened: boolean
  onClose: () => void
  category?: CategoryDto
}

export function CategoryFormModal({ opened, onClose, category }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={category ? 'Kategoriyi düzenle' : 'Yeni kategori'}
    >
      {/* Modal content unmounts on close, so every open starts with fresh form state. */}
      <CategoryForm category={category} onClose={onClose} />
    </Modal>
  )
}

function CategoryForm({ category, onClose }: { category?: CategoryDto; onClose: () => void }) {
  const save = useSaveCategory()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: category?.name ?? '' },
  })

  const onSubmit = handleSubmit(async ({ name }) => {
    setFormError(null)
    try {
      await save.mutateAsync({ id: category?.id, name })
      notifications.show({
        color: 'green',
        message: category ? 'Kategori güncellendi.' : 'Kategori oluşturuldu.',
      })
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['name']))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        {formError && <Alert color="red">{formError}</Alert>}
        <TextInput
          label="Kategori adı"
          required
          data-autofocus
          error={errors.name?.message}
          {...register('name')}
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
