import { Outlet } from 'react-router-dom'
import { DesktopSidebar } from './DesktopSidebar'
import { MobileBottomNav } from './MobileBottomNav'

export function AppShell() {
  return (
    <div className="flex min-h-svh w-full">
      <DesktopSidebar />
      <main className="flex-1 overflow-y-auto pb-16 md:pb-0">
        <Outlet />
      </main>
      <MobileBottomNav />
    </div>
  )
}
