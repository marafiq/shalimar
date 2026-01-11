import { Outlet, createRootRoute } from '@tanstack/react-router'
import { AppShell } from './ui/AppShell'
import { NotificationsPanel } from './ui/NotificationsPanel'
import { ToastHost } from './ui/Toasts'
import { ModalHost } from './ui/ModalHost'

export const Route = createRootRoute({
    component: RootComponent,
})

function RootComponent() {
    return (
        <>
            <AppShell>
                <Outlet />
            </AppShell>
            <NotificationsPanel />
            <ToastHost />
            <ModalHost />
        </>
    )
}
