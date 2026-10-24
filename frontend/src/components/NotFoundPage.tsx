import { Button, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <Stack align="flex-start">
      <Title order={2}>Sayfa bulunamadı</Title>
      <Text c="dimmed">Aradığınız sayfa mevcut değil.</Text>
      <Button component={Link} to="/" variant="light">
        Ana sayfaya dön
      </Button>
    </Stack>
  )
}
