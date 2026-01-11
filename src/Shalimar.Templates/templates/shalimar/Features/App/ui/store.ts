import { Store } from '@tanstack/store'
import type { Id } from '../../Crm/types'

export type ToastTone = 'neutral' | 'success' | 'warning' | 'danger'

export interface ToastItem {
    id: Id
    ts: string
    tone: ToastTone
    title: string
    message?: string
}

export interface NotificationItem {
    id: Id
    ts: string
    title: string
    message?: string
    href?: string
    read: boolean
}

export interface UiState {
    mobileNavOpen: boolean
    notificationsOpen: boolean
    sidebarCollapsed: boolean
    toasts: ToastItem[]
    notifications: NotificationItem[]
    modal: null | { title: string; body: string }
}

function nowIso() {
    return new Date().toISOString()
}

function makeId(prefix: string) {
    return `${prefix}_${Math.random().toString(16).slice(2)}_${Date.now().toString(16)}`
}

export const uiStore = new Store<UiState>({
    mobileNavOpen: false,
    notificationsOpen: false,
    sidebarCollapsed: false,
    toasts: [],
    notifications: [
        {
            id: 'n_welcome',
            ts: nowIso(),
            title: 'Welcome',
            message: 'This is a mock notification panel (right drawer) to prove the global UI pattern.',
            href: '/',
            read: false,
        },
    ],
    modal: null,
})

export function setMobileNavOpen(open: boolean) {
    uiStore.setState((s) => ({ ...s, mobileNavOpen: open }))
}

export function toggleMobileNav() {
    uiStore.setState((s) => ({ ...s, mobileNavOpen: !s.mobileNavOpen }))
}

export function setNotificationsOpen(open: boolean) {
    uiStore.setState((s) => ({ ...s, notificationsOpen: open }))
}

export function toggleNotifications() {
    uiStore.setState((s) => ({ ...s, notificationsOpen: !s.notificationsOpen }))
}

export function toggleSidebarCollapsed() {
    uiStore.setState((s) => ({ ...s, sidebarCollapsed: !s.sidebarCollapsed }))
}

export function pushToast(toast: Omit<ToastItem, 'id' | 'ts'>) {
    const item: ToastItem = { ...toast, id: makeId('toast'), ts: nowIso() }
    uiStore.setState((s) => ({ ...s, toasts: [item, ...s.toasts].slice(0, 5) }))
    // Auto-dismiss; keep it long enough for screenshots, but not forever.
    window.setTimeout(() => dismissToast(item.id), 10_000)
}

export function dismissToast(id: Id) {
    uiStore.setState((s) => ({ ...s, toasts: s.toasts.filter((t) => t.id !== id) }))
}

export function pushNotification(n: Omit<NotificationItem, 'id' | 'ts' | 'read'>) {
    const item: NotificationItem = { ...n, id: makeId('n'), ts: nowIso(), read: false }
    uiStore.setState((s) => ({ ...s, notifications: [item, ...s.notifications].slice(0, 50) }))
}

export function markAllNotificationsRead() {
    uiStore.setState((s) => ({ ...s, notifications: s.notifications.map((n) => ({ ...n, read: true })) }))
}

export function openModal(title: string, body: string) {
    uiStore.setState((s) => ({ ...s, modal: { title, body } }))
}

export function closeModal() {
    uiStore.setState((s) => ({ ...s, modal: null }))
}

