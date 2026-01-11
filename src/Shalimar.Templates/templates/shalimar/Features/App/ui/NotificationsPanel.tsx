import { Link } from '@tanstack/react-router'
import { useStore } from '@tanstack/react-store'
import { Button, Heading, Text } from '@react-spectrum/s2'
import { crmStore } from '../../Crm/store'
import { IconX } from './icons'
import { markAllNotificationsRead, setNotificationsOpen, uiStore } from './store'

export function NotificationsPanel() {
    const ui = useStore(uiStore)
    const crm = useStore(crmStore)

    if (!ui.notificationsOpen) return null

    const unread = ui.notifications.filter((n) => !n.read).length

    return (
        <div className="fixed inset-0 z-40" aria-label="Notifications panel">
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={() => setNotificationsOpen(false)} />
            <aside className="absolute right-0 top-0 h-full w-[min(420px,100vw)] border-l border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
                <div className="flex h-full flex-col">
                    <div className="flex items-start justify-between gap-3 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
                        <div>
                            <Heading level={3}>Notifications</Heading>
                            <div className="text-sm text-zinc-600 dark:text-zinc-300">
                                {unread ? `${unread} unread` : 'All caught up'}
                            </div>
                        </div>
                        <Button isQuiet onPress={() => setNotificationsOpen(false)} aria-label="Close notifications">
                            <IconX />
                        </Button>
                    </div>

                    <div className="flex-1 overflow-auto p-4">
                        {ui.notifications.length === 0 ? (
                            <div className="rounded-xl border border-zinc-200 bg-white p-4 text-sm text-zinc-600 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-300">
                                No notifications yet.
                            </div>
                        ) : (
                            <div className="space-y-2">
                                {ui.notifications.map((n) => (
                                    <div
                                        key={n.id}
                                        className={`rounded-xl border p-3 ${
                                            n.read
                                                ? 'border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40'
                                                : 'border-blue-500/30 bg-blue-500/10 shadow-sm'
                                        }`}
                                    >
                                        <div className="flex items-start justify-between gap-3">
                                            <div className="min-w-0">
                                                <div className="truncate text-sm font-semibold">{n.title}</div>
                                                {n.message ? (
                                                    <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">{n.message}</div>
                                                ) : null}
                                                <div className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">
                                                    {new Date(n.ts).toLocaleTimeString()}
                                                </div>
                                            </div>
                                            {n.href ? (
                                                <Button elementType={Link as any} to={n.href} isQuiet>
                                                    Open
                                                </Button>
                                            ) : null}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}

                        <div className="mt-6">
                            <Heading level={4}>Live activity</Heading>
                            <div className="mt-2 space-y-2">
                                {crm.activity.slice(0, 12).map((a) => (
                                    <div
                                        key={a.id}
                                        className="rounded-xl border border-zinc-200 bg-white p-3 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40"
                                    >
                                        <div className="text-xs text-zinc-500 dark:text-zinc-400">
                                            {new Date(a.ts).toLocaleTimeString()}
                                        </div>
                                        <div className="mt-1">{a.summary}</div>
                                    </div>
                                ))}
                                {crm.activity.length === 0 ? (
                                    <Text>
                                        <span className="text-sm opacity-70">No activity.</span>
                                    </Text>
                                ) : null}
                            </div>
                        </div>
                    </div>

                    <div className="border-t border-zinc-200 p-4 dark:border-zinc-800">
                        <div className="flex items-center justify-between gap-2">
                            <Button isQuiet onPress={markAllNotificationsRead}>
                                Mark all read
                            </Button>
                            <Button isQuiet onPress={() => setNotificationsOpen(false)}>
                                Close
                            </Button>
                        </div>
                    </div>
                </div>
            </aside>
        </div>
    )
}

