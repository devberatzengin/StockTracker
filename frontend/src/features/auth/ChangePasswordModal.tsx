import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Group, Modal, PasswordInput, Stack } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { getErrorMessage, isApiError } from '@/api/client'
import { useAuth } from '@/features/auth/authContext'
import { passwordSchema } from '@/features/auth/passwordSchema'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Mevcut şifre zorunludur.'),
    newPassword: passwordSchema,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Şifreler eşleşmiyor.',
  })
  .refine((v) => v.newPassword !== v.currentPassword, {
    path: ['newPassword'],
    message: 'Yeni şifre mevcut şifreden farklı olmalıdır.',
  })

type FormValues = z.infer<typeof schema>

const emptyValues: FormValues = { currentPassword: '', newPassword: '', confirmPassword: '' }

export function ChangePasswordModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  return (
    <Modal opened={opened} onClose={onClose} title="Şifre değiştir">
      <ChangePasswordForm onClose={onClose} />
    </Modal>
  )
}

function ChangePasswordForm({ onClose }: { onClose: () => void }) {
  const { changePassword } = useAuth()
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError: setFieldError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: emptyValues })

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setError(null)
    try {
      await changePassword(currentPassword, newPassword)
      notifications.show({ color: 'green', message: 'Şifreniz değiştirildi.' })
      onClose()
    } catch (e) {
      const currentPasswordError = isApiError(e, 400) ? e.errors?.currentPassword?.[0] : undefined
      if (currentPasswordError) {
        setFieldError('currentPassword', { message: currentPasswordError })
      } else {
        setError(getErrorMessage(e))
      }
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        {error && <Alert color="red">{error}</Alert>}
        <PasswordInput
          label="Mevcut şifre"
          autoComplete="current-password"
          error={errors.currentPassword?.message}
          {...register('currentPassword')}
        />
        <PasswordInput
          label="Yeni şifre"
          description="En az 8 karakter; büyük harf, küçük harf ve rakam içermeli."
          autoComplete="new-password"
          error={errors.newPassword?.message}
          {...register('newPassword')}
        />
        <PasswordInput
          label="Yeni şifre (tekrar)"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register('confirmPassword')}
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
