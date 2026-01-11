export function TasksGridSkeleton() {
    return (
        <div className="space-y-3" data-testid="tasks-grid-skeleton">
            <div className="h-4 w-48 rounded bg-zinc-200 dark:bg-zinc-800" />
            <div className="overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="space-y-0">
                    {Array.from({ length: 6 }).map((_, i) => (
                        <div key={i} className="flex gap-3 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
                            <div className="h-4 w-[55%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="h-4 w-[15%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="h-4 w-[15%] rounded bg-zinc-200 dark:bg-zinc-800" />
                            <div className="ml-auto h-4 w-16 rounded bg-zinc-200 dark:bg-zinc-800" />
                        </div>
                    ))}
                </div>
            </div>
        </div>
    )
}

