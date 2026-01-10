import { createFileRoute } from '@tanstack/react-router'
import Home from './index'

export const Route = createFileRoute('/')({
    component: HomeRoute,
})

function HomeRoute() {
    const props = window.__SHALIMAR_PROPS__ ?? { message: 'Welcome to Shalimar' }
    return <Home message={props.message} />
}
