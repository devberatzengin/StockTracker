import { zodResolver } from '@hookform/resolvers/zod'
import {
  Alert,
  Button,
  Center,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { IconAlertTriangle } from '@tabler/icons-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate, type Location } from 'react-router'
import { z } from 'zod'
import { getErrorMessage } from '@/api/client'
import { FullPageLoader } from '@/components/FullPageLoader'
import { useAuth } from '@/features/auth/authContext'

const schema = z.object({
  email: z.string().trim().min(1, 'E-posta zorunludur.'),
  password: z.string().min(1, 'Şifre zorunludur.'),
})

type FormValues = z.infer<typeof schema>

function returnPath(location: Location) {
  const from = (location.state as { from?: Location } | null)?.from
  return from && from.pathname !== '/login' ? `${from.pathname}${from.search}${from.hash}` : '/'
}

export function LoginPage() {
  const { status, login } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  if (status === 'loading') {
    return <FullPageLoader />
  }

  if (status === 'authenticated') {
    return <Navigate to={returnPath(location)} replace />
  }

  const onSubmit = handleSubmit(async ({ email, password }) => {
    setError(null)
    try {
      await login(email, password)
      navigate(returnPath(location), { replace: true })
    } catch (e) {
      setError(getErrorMessage(e))
    }
  })

  return (
    <Center mih="100vh" p="md">
      <Paper withBorder shadow="sm" p="xl" w="100%" maw={380}>
        <form onSubmit={onSubmit} noValidate>
          <Stack>
            <div>
              <Title order={3}>StockTracker</Title>
              <Text c="dimmed" size="sm">
                Devam etmek için giriş yapın.
              </Text>
            </div>

            {error && (
              <Alert color="red" icon={<IconAlertTriangle size={18} />}>
                {error}
              </Alert>
            )}

            <TextInput
              label="E-posta"
              type="email"
              autoComplete="username"
              autoFocus
              error={errors.email?.message}
              {...register('email')}
            />
            <PasswordInput
              label="Şifre"
              autoComplete="current-password"
              error={errors.password?.message}
              {...register('password')}
            />
            <Button type="submit" loading={isSubmitting} fullWidth>
              Giriş yap
            </Button>
          </Stack>
        </form>
      </Paper>
    </Center>
  )
}
