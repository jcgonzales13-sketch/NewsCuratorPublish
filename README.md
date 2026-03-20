# AI News Curator for LinkedIn

MVP em .NET 8 para coletar noticias de IA via RSS, persistir em SQLite, aplicar curadoria automatizada, gerar draft em portugues para LinkedIn e operar em modo manual ou automatico.

## Estrutura

- `src/AiNewsCurator.Api`: endpoints internos, healthcheck e aprovacao manual.
- `src/AiNewsCurator.Worker`: scheduler diario com `BackgroundService`.
- `src/AiNewsCurator.Application`: orquestracao do pipeline e regras de aplicacao.
- `src/AiNewsCurator.Domain`: entidades, enums, regras e interfaces.
- `src/AiNewsCurator.Infrastructure`: SQLite, RSS, servicos de IA e adaptador do LinkedIn.
- `tests/`: testes unitarios e esqueleto de integracao.

## Fluxo do MVP

1. Inicializa schema SQLite e semeia duas fontes RSS padrao.
2. Coleta noticias recentes das fontes ativas.
3. Normaliza URL e hashes para deduplicacao.
4. Avalia relevancia e gera draft com um servico heuristico de IA.
5. Valida o texto do post.
6. Salva draft para aprovacao manual ou aprova automaticamente.
7. Publica no LinkedIn via adaptador isolado quando solicitado.

## Configuracao

Use o arquivo `.env.example` como referencia. As principais variaveis:

- `DATABASE_PATH`: caminho do arquivo SQLite. No Render use persistent disk, por exemplo `/var/data/ainews/ainews.db`.
- `PUBLISH_MODE`: `Manual` ou `Automatic`.
- `INTERNAL_API_KEY`: chave exigida em `/internal/*` via header `X-API-Key`.
- `LINKEDIN_ACCESS_TOKEN` e `LINKEDIN_MEMBER_URN`: necessarios para publicacao real.
- `AI_PROVIDER`: no MVP o valor padrao `Heuristic` usa um avaliador local sem custo.

O projeto ja aceita diretamente os nomes da spec em ambiente, como `DATABASE_PATH`, `PUBLISH_MODE`, `INTERNAL_API_KEY`, `LINKEDIN_ACCESS_TOKEN` e `AI_PROVIDER`.

Para execucao local com segredos fora do projeto, copie [.env.local.example](/c:/PublishNews/.env.local.example) para `.env.local`, preencha as chaves e use [run-api-local.sh](/c:/PublishNews/scripts/run-api-local.sh).

## Execucao local

```bash
dotnet build PublishNews.sln
dotnet test PublishNews.sln
dotnet run --project src/AiNewsCurator.Api
```

Em outro terminal:

```bash
dotnet run --project src/AiNewsCurator.Worker
```

Tambem existe uma UI operacional minima em `GET /ops`. Ela pede a mesma `INTERNAL_API_KEY`, mas por uma tela de login simples, e permite:

- rodar coleta, curadoria e rotina diaria
- revisar drafts pendentes
- publicar drafts aprovados
- cadastrar e ativar/desativar fontes
- acompanhar execucoes recentes e status do LinkedIn

## Endpoints internos

Todos exigem `X-API-Key`.

- `GET /health`
- `POST /internal/run/daily`
- `POST /internal/run/collect`
- `POST /internal/run/curate`
- `POST /internal/run/publish/{draftId}`
- `GET /internal/drafts`
- `GET /internal/news`
- `GET /internal/news/{id}`
- `POST /internal/news/{id}/reprocess`
- `GET /internal/sources`
- `POST /internal/sources`
- `PUT /internal/sources/{id}`
- `POST /internal/sources/{id}/deactivate`
- `GET /internal/auth/linkedin/status`
- `POST /internal/auth/linkedin/start`
- `POST /internal/auth/linkedin/validate`
- `GET /internal/auth/linkedin/callback`
- `POST /internal/drafts/{id}/approve`
- `POST /internal/drafts/{id}/reject`
- `GET /internal/runs`

Exemplo:

```bash
curl -X POST http://localhost:5138/internal/run/daily -H "X-API-Key: changeme"
```

Para iniciar a conexao OAuth do LinkedIn em ambiente local:

```bash
curl -X POST http://localhost:5138/internal/auth/linkedin/start -H "X-API-Key: changeme"
```

Use a URL retornada no navegador. O callback esperado para desenvolvimento local e `http://localhost:5138/internal/auth/linkedin/callback`.

## Deploy no Render

O repositório agora inclui [render.yaml](/c:/PublishNews/render.yaml) com o desenho recomendado para SQLite no MVP: um unico `Web Service` hospedando a API e o scheduler interno no mesmo processo.

Motivo: no Render, o persistent disk e por servico. Com SQLite, separar API e worker em servicos diferentes introduz risco de cada processo enxergar um filesystem diferente.

Passos:

1. Criar o servico a partir do blueprint `render.yaml`.
2. Manter `ENABLE_SCHEDULER=true` no servico web para a rotina diaria rodar internamente.
3. Configurar segredos como `INTERNAL_API_KEY`, `AI_API_KEY`, `LINKEDIN_ACCESS_TOKEN` e `LINKEDIN_MEMBER_URN`.
4. Confirmar o mount do disco em `/var/data/ainews`.
5. Validar o healthcheck em `/health`.

Nao use cron job separado do Render acessando SQLite local, porque o persistent disk nao e compartilhado da forma necessaria para esse desenho.

O projeto `AiNewsCurator.Worker` continua util para outros ambientes, mas no Render com SQLite a topologia recomendada e o host web unico.

## Docker

O repositorio agora inclui [Dockerfile](/c:/PublishNews/Dockerfile) e [.dockerignore](/c:/PublishNews/.dockerignore) para publicar a `Api` em container.

Build local:

```bash
docker build -t ai-news-curator .
```

Execucao local:

```bash
docker run --rm -p 8080:8080 \
  -e INTERNAL_API_KEY=changeme \
  -e DATABASE_PATH=/var/data/ainews/ainews.db \
  ai-news-curator
```

No Render, basta usar o `Dockerfile` da raiz. O app escuta na porta `8080` dentro do container e o healthcheck continua em `/health`.

## Limitacoes atuais

- Curadoria por IA usa heuristica local por padrao, mas agora pode usar `AI_PROVIDER=OpenAI` com `AI_API_KEY`.
- O fluxo OAuth local do LinkedIn agora esta implementado, mas refresh token automatico ainda nao foi implementado.
- Similaridade semantica avancada ainda nao foi adicionada.
- O projeto de integracao existe como base, mas os testes de integracao completos ainda ficam para a proxima iteracao.
