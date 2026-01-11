import { Button } from '@react-spectrum/s2'
import { useDeferred } from '@shalimar/runtime'
import type { CrmTasksGridDto } from '@generated/types'

export function TasksGrid(props: { href: string; onOpenTask: (id: string) => void; onPageChange: (page: number) => void }) {
    const data = useDeferred<CrmTasksGridDto>({ href: props.href })
    const pages = Math.max(1, Math.ceil(data.total / data.pageSize))

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between gap-3">
                <div className="text-sm text-zinc-600 dark:text-zinc-300">
                    {data.total} total • page {data.page} / {pages}
                </div>
                <div className="flex gap-2">
                    <Button isQuiet isDisabled={data.page <= 1} onPress={() => props.onPageChange(data.page - 1)}>
                        Prev
                    </Button>
                    <Button isQuiet isDisabled={data.page >= pages} onPress={() => props.onPageChange(data.page + 1)}>
                        Next
                    </Button>
                </div>
            </div>

            <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <table className="w-full text-sm">
                    <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-950/60 dark:text-zinc-400">
                        <tr>
                            <th className="px-4 py-3">Title</th>
                            <th className="px-4 py-3">Status</th>
                            <th className="px-4 py-3">Priority</th>
                            <th className="px-4 py-3">Updated</th>
                            <th className="px-4 py-3"></th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                        {data.items.map((t) => (
                            <tr key={t.id} data-testid="tasks-row">
                                <td className="px-4 py-3">
                                    <div className="font-medium">{t.title}</div>
                                    <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">{t.id}</div>
                                </td>
                                <td className="px-4 py-3">{t.status.replaceAll('_', ' ')}</td>
                                <td className="px-4 py-3">{t.priority}</td>
                                <td className="px-4 py-3">{new Date(t.updatedAt).toLocaleString()}</td>
                                <td className="px-4 py-3 text-right">
                                    <Button isQuiet onPress={() => props.onOpenTask(t.id)}>
                                        Open
                                    </Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    )
}

