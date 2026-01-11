export function PageSkeleton(props: { title?: string }) {
    return (
        <div className="space-y-4" data-testid="page-skeleton">
            <div className="flex items-end justify-between gap-4">
                <div className="space-y-2">
                    <div className="h-7 w-52 rounded-md bg-black/10 dark:bg-white/10" />
                    <div className="h-4 w-80 max-w-[70vw] rounded-md bg-black/10 dark:bg-white/10" />
                </div>
                <div className="h-9 w-28 rounded-md bg-black/10 dark:bg-white/10" />
            </div>
            <div className="grid gap-3 lg:grid-cols-3">
                <div className="h-28 rounded-xl bg-black/10 dark:bg-white/10" />
                <div className="h-28 rounded-xl bg-black/10 dark:bg-white/10" />
                <div className="h-28 rounded-xl bg-black/10 dark:bg-white/10" />
            </div>
            <div className="h-64 rounded-xl bg-black/10 dark:bg-white/10" />
            <div className="h-64 rounded-xl bg-black/10 dark:bg-white/10" />
        </div>
    )
}

export function DrawerSkeleton() {
    return (
        <div className="space-y-4" data-testid="drawer-skeleton">
            <div className="h-7 w-64 rounded-md bg-black/10 dark:bg-white/10" />
            <div className="h-4 w-80 rounded-md bg-black/10 dark:bg-white/10" />
            <div className="h-10 w-full rounded-md bg-black/10 dark:bg-white/10" />
            <div className="h-10 w-full rounded-md bg-black/10 dark:bg-white/10" />
            <div className="h-64 w-full rounded-xl bg-black/10 dark:bg-white/10" />
        </div>
    )
}

