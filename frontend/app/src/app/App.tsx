import { RouterProvider } from 'react-router-dom'
import { useSessionBootstrap } from '@/features/auth/hooks/useSessionBootstrap'
import { setupAuthBootstrap } from './authBootstrap'
import { router } from './router'

// Registrado uma única vez, na carga do módulo — antes de qualquer
// requisição (inclusive o refresh silencioso do boot, abaixo).
setupAuthBootstrap()

function App() {
  const { isBootstrapping } = useSessionBootstrap()

  if (isBootstrapping) {
    return null
  }

  return <RouterProvider router={router} />
}

export default App
