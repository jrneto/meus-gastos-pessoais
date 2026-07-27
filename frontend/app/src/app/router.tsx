import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { ExpensesListPage } from '@/routes/ExpensesListPage'
import { LoginPage } from '@/routes/LoginPage'
import { RegisterExpensePage } from '@/routes/RegisterExpensePage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <ProtectedRoute />,
    children: [
      { index: true, element: <RegisterExpensePage /> },
      { path: 'expenses', element: <ExpensesListPage /> },
    ],
  },
])
