import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/tasks/$taskId')({
    loader: ({ params }) => {
        throw redirect({ to: '/tasks', search: { drawer: params.taskId } })
    },
})
