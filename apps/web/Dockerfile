FROM node:24.22.1-slim AS build
WORKDIR /src
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc* ./
COPY apps/web/package.json apps/web/
COPY packages/contracts/package.json packages/contracts/
RUN pnpm install --frozen-lockfile
COPY packages/contracts/ packages/contracts/
COPY apps/web/ apps/web/
RUN pnpm -C apps/web build

FROM node:24.22.1-slim
WORKDIR /app
COPY --from=build --chown=node:node /src/apps/web/.output ./.output
ENV PORT=3000 NODE_ENV=production
EXPOSE 3000
USER node
CMD ["node", ".output/server/index.mjs"]
