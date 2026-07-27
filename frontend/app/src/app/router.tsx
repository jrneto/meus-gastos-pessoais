import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AppShell } from '@/components/nav/AppShell'
import { ExpensesListPage } from '@/routes/ExpensesListPage'
import { HomePage } from '@/routes/HomePage'
import { LoginPage } from '@/routes/LoginPage'
import { RegisterExpensePage } from '@/routes/RegisterExpensePage'
import { SettingsPage } from '@/routes/SettingsPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'expenses/new', element: <RegisterExpensePage /> },
          { path: 'expenses', element: <ExpensesListPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
