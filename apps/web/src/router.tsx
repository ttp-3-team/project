import { QueryClient } from '@tanstack/react-query'
import { createRouter as createTanStackRouter } from '@tanstack/react-router'
import { setupRouterSsrQueryIntegration } from '@tanstack/react-router-ssr-query'
import { routeTree } from './routeTree.gen'
import { ErrorState, NotFoundState, LoadingState } from './components/states'

/** Create the router with a per-request query client (SSR-safe). */
export function getRouter() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 10_000 } } })
  const router = createTanStackRouter({
    routeTree,
    context: { queryClient },
    scrollRestoration: true,
    defaultPreload: 'intent',
    defaultPreloadStaleTime: 0,
    defaultErrorComponent: ErrorState,
    defaultNotFoundComponent: NotFoundState,
    defaultPendingComponent: LoadingState,
  })
  setupRouterSsrQueryIntegration({ router, queryClient })
  return router
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
