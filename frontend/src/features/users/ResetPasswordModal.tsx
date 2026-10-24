import { zodResolver } from '@hookform/resolvers/zod'
import { Alert, Button, Group, Modal, PasswordInput, Stack, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { applyServerErrors } from '@/api/formErrors'
import type { UserDto } from '@/api/types'
import { passwordSchema } from '@/features/auth/passwordSchema'
import { useResetPassword } from '@/features/users/usersApi'

const schema = z.object({ newPassword: passwordSchema })
type FormValues = z.infer<typeof schema>

export function ResetPasswordModal({
  user,
  onClose,
}: {
  user: UserDto | null
  onClose: () => void
}) {
  return (
    <Modal opened={!!user} onClose={onClose} title="Şifre sıfırla">
      {/* Modal content unmounts on close, so every open starts with fresh form state. */}
      {user && <ResetPasswordForm user={user} onClose={onClose} />}
    </Modal>
  )
}

function ResetPasswordForm({ user, onClose }: { user: UserDto; onClose: () => void }) {
  const resetPassword = useResetPassword()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { newPassword: '' } })

  const onSubmit = handleSubmit(async ({ newPassword }) => {
    try {
      await resetPassword.mutateAsync({ id: user.id, newPassword })
      notifications.show({ color: 'green', message: 'Şifre sıfırlandı.' })
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['newPassword']))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        <Text size="sm">
          <b>{user.fullName}</b> için yeni şifre belirleyin. Kullanıcının açık oturumları kapatılır.
        </Text>
        {formError && <Alert color="red">{formError}</Alert>}
        <PasswordInput
          label="Yeni şifre"
          description="En az 8 karakter; büyük harf, küçük harf ve rakam içermeli."
          autoComplete="new-password"
          error={errors.newPassword?.message}
          {...register('newPassword')}
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Sıfırla
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
