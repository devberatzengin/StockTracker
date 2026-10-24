import { Button, Stack, Text, Title } from '@mantine/core'
import { IconLock } from '@tabler/icons-react'
import { Link } from 'react-router'

export function ForbiddenPage() {
  return (
    <Stack align="flex-start">
      <IconLock size={40} stroke={1.5} />
      <Title order={2}>Erişim yetkiniz yok</Title>
      <Text c="dimmed">Bu sayfayı görüntülemek için gerekli yetkiye sahip değilsiniz.</Text>
      <Button component={Link} to="/" variant="light">
        Ana sayfaya dön
      </Button>
    </Stack>
  )
}
