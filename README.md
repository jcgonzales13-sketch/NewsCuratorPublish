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

## Endpoints internos

Todos exigem `X-API-Key`.

- `GET /health`
- `POST /internal/run/daily`
- `POST /internal/run/collect`
- `POST /internal/run/curate`
- `POST /internal/run/publish/{draftId}`
- `GET /internal/drafts`
- `POST /internal/drafts/{id}/approve`
- `POST /internal/drafts/{id}/reject`
- `GET /internal/runs`

Exemplo:

```bash
curl -X POST http://localhost:5138/internal/run/daily -H "X-API-Key: changeme"
```

## Deploy no Render

Recomendado:

1. Criar um `Web Service` para `AiNewsCurator.Api`.
2. Criar um `Background Worker` para `AiNewsCurator.Worker`.
3. Anexar persistent disk ao servico que acessa o SQLite.
4. Configurar `DatabasePath=/var/data/ainews/ainews.db`.
5. Definir todas as variaveis sensiveis no painel do Render.

Nao use cron job separado do Render acessando SQLite local, porque o persistent disk nao e compartilhado da forma necessaria para esse desenho.

## Limitacoes atuais

- Curadoria por IA usa heuristica local por padrao, sem provider real conectado.
- Publicacao no LinkedIn assume token de acesso preexistente; refresh token nao foi implementado.
- Similaridade semantica avancada ainda nao foi adicionada.
- O projeto de integracao existe como base, mas os testes de integracao completos ainda ficam para a proxima iteracao.
