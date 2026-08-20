# Task 17 — Higienizar a impressão da nota fiscal

> Renumerada de "Task 16" para "Task 17" durante o diagnóstico: a Task 16
> (`Documentação visual e execução assistida`) já existia em `docs/progress.md`, concluída no
> commit `f6a5641`. A baseline informada no pedido (`HEAD d9f64b8`) também estava desatualizada —
> o HEAD real já incluía esse checkpoint. Ambas as divergências foram confirmadas com o usuário
> antes de qualquer alteração.

## Problema

Ao imprimir e fechar uma nota fiscal, a impressão resultante podia conter:

1. O toast verde "Nota Nº X impressa e fechada com sucesso" dentro do papel impresso.
2. `Status: Aberta`, mesmo com a operação já confirmada e a nota já fechada no backend.
3. Cabeçalhos/rodapés adicionados pelo navegador (data, título, URL, número de página) — estes
   últimos fora do controle da aplicação.

## Causa raiz

`InvoiceDetailPage.handlePrintSuccess` (`invoice-detail-page.ts`) executava, na ordem antiga:

```typescript
this.invoice.set(invoice); // 1. atualiza o signal
this.notification.success(...); // 2. abre o snack bar (toast)
window.print(); // 3. abre o diálogo de impressão
```

Dois problemas nessa ordem:

- **`Status: Aberta` na impressão**: `signal.set()` não garante que o Angular já tenha propagado a
  mudança para o DOM no mesmo tick. `window.print()` podia capturar `app-invoice-print-view` ainda
  com o snapshot anterior ao fechamento.
- **Toast impresso**: `notification.success()` (via `MatSnackBar`) anexa seu elemento ao
  `.cdk-overlay-container` (fora da árvore de qualquer componente, diretamente sob `<body>`)
  sincronamente, antes de `window.print()` ser chamado — e não existia nenhuma regra em
  `@media print` escondendo `.cdk-overlay-container`, apenas a navegação do shell
  (`.app-shell__topbar`).

Um teste existente (`invoice-detail-page.spec.ts`, removido nesta task) chegava a **afirmar** essa
ordem como correta, comprovando que o toast aparecer antes da impressão era o comportamento
intencional na implementação anterior — não um bug de teste, um bug de produto.

## Escopo

- `src/frontend/invoice-web/src/app/features/invoices/invoice-detail/invoice-detail-page.ts`
- `src/frontend/invoice-web/src/styles.scss` (regra central `@media print`)
- Testes: `invoice-detail-page.spec.ts` (ajustado), `styles.print.spec.ts` (novo)
- `docs/technical-details.md` (trecho de impressão)
- `docs/progress.md`, `docs/tasks/task-17-clean-invoice-print.md` (este arquivo)

## Fora de escopo

- Backend (`Billing.Api`, `Inventory.Api`), banco de dados, migrations, contratos HTTP.
- Regra de fechamento da nota ou baixa de estoque.
- Idempotência, concorrência ou resiliência.
- Remoção dos cabeçalhos/rodapés do navegador (data, título, URL, número de página) — controlados
  pelo navegador, não pela aplicação; documentado como observação, não "corrigido".
- Dependências novas.

## Critérios de aceite

- Após o sucesso do backend, o estado local é atualizado (`status = Closed`, `closedAtUtc`
  preenchido) **e sincronizado com o DOM** antes de `window.print()` ser chamado.
- A nota impressa sempre mostra `Status: Fechada`.
- Nenhum toast, snackbar, overlay do CDK, toolbar, navegação, botão ou conteúdo da tela normal
  aparece na impressão.
- O toast de sucesso não é mostrado antes da impressão — só depois que a janela de impressão do
  navegador for encerrada (evento `afterprint`), exatamente uma vez.
- Em caso de erro: `window.print()` não é chamado, o toast de sucesso não é chamado, permanece
  exatamente um toast de erro.
- Clique duplicado continua impedido (comportamento pré-existente, não tocado).
- A regra CSS de proteção de impressão é central (um único bloco `@media print` em
  `src/styles.scss`), sem `::ng-deep`, sem regras espalhadas por componente.
- `InvoicePrintView` continua visível e formatado corretamente na impressão.

## Solução adotada

1. **Sincronização de renderização antes de imprimir**: `ChangeDetectorRef.detectChanges()`,
   injetado em `InvoiceDetailPage`, chamado logo após `this.invoice.set(invoice)` e antes de
   `window.print()`. Como `app-invoice-print-view` é um nó irmão dentro do mesmo template de
   `InvoiceDetailPage` (não uma rota separada nem um portal), uma única chamada de
   `detectChanges()` no componente pai já sincroniza os dois. Essa é uma opção de "detecção de
   mudança explícita e justificada" (uma das opções aceitáveis do pedido) — preferida a
   `afterNextRender`/`requestAnimationFrame` porque é síncrona, determinística, e trivialmente
   testável sem precisar de hooks de pós-renderização do Angular ou de mecanismos de agendamento
   assíncrono, que agregariam complexidade sem benefício aqui (view sempre presente no mesmo
   template, nunca lazy-carregada).
2. **Toast adiado até o fim da impressão**: `window.addEventListener('afterprint', handler)`
   registrado antes de `window.print()`; o `handler` remove a si mesmo e só então chama
   `notification.success(...)`. `afterprint` é o único sinal confiável entre navegadores de que a
   caixa de diálogo de impressão foi fechada — `window.print()` bloqueia a execução do script em
   alguns navegadores (Chrome) mas não em outros (Firefox), então "logo depois da chamada" não é
   um sinal cross-browser válido. Nenhum `setTimeout`/atraso fixo foi usado. O listener também é
   removido em `DestroyRef.onDestroy` para não vazar caso o usuário saia da página com o diálogo
   ainda aberto.
3. **Regra CSS central**: `src/styles.scss`, dentro do único bloco `@media print` já existente,
   nova regra `.cdk-overlay-container, .cdk-overlay-backdrop { display: none !important; }`. Cobre
   snackbars, diálogos, menus e tooltips do Angular Material/CDK com uma única regra, porque o CDK
   sempre anexa esses overlays diretamente sob `<body>`, fora da árvore de qualquer componente —
   por isso um seletor global simples é suficiente, sem `::ng-deep` e sem tocar em nenhum
   componente individualmente.

## Diferença entre conteúdo da aplicação e cabeçalhos do navegador

- **Conteúdo da aplicação** (responsabilidade desta task): toast, overlays, navegação, botões —
  tudo isso é HTML/CSS gerado pelo Angular, então pode (e deve) ser escondido via CSS de impressão.
- **Cabeçalhos/rodapés do navegador** (fora de escopo, não controlável pela aplicação): data/hora,
  título da página, URL e número de página são adicionados pelo próprio navegador na janela de
  impressão — nenhuma técnica confiável do lado da aplicação os remove entre navegadores diferentes
  (e alterar o `<title>` global só para a duração da impressão foi explicitamente descartado como
  solução, por ser um hack frágil e por afetar o restante da aplicação). Documentado como
  observação para o usuário final: desmarcar "Cabeçalhos e rodapés" em "Mais configurações" na
  janela de impressão do navegador.

## Testes

### `invoice-detail-page.spec.ts` (ajustado)

- O teste antigo que afirmava `window.print()` ocorrendo *depois* do toast foi reescrito — a
  ordem correta agora é a inversa.
- Novo: no momento exato em que `window.print()` é chamado (inspecionado de dentro do próprio mock
  de `window.print`), o DOM de `app-invoice-print-view` já mostra `Fechada` (nunca `Aberta`).
- Novo: `notification.success` não é chamado antes de `window.print()`; é chamado exatamente uma
  vez, com a mensagem correta, somente após `window.dispatchEvent(new Event('afterprint'))`.
- Novo: disparar `afterprint` mais de uma vez não duplica o toast.
- Ajustado: um teste de erro (409 sem `errorCode`) agora também confirma explicitamente que
  `notification.success` nunca é chamado.
- Pré-existentes, sem alteração de comportamento: botão habilitado/desabilitado por status,
  spinner durante a chamada, chamada HTTP única mesmo com cliques duplicados, mensagens de erro
  por `errorCode`, reabilitação do botão após falha.

### `styles.print.spec.ts` (novo)

Compila o `src/styles.scss` real (via `styleUrl` em um componente de teste com
`ViewEncapsulation.None`, sem duplicar CSS em nenhum arquivo à parte — evita que o teste fique
dessincronizado do arquivo real) e inspeciona o `CSSOM` resultante (`document.styleSheets`) para
confirmar, dentro do bloco `@media print`:

- a regra `.cdk-overlay-container { display: none }` existe;
- a regra `.cdk-overlay-backdrop` existe;
- `.app-shell__topbar` continua escondida;
- nenhuma regra esconde `app-invoice-print-view`.

### `invoice-print-view.spec.ts`

Sem alteração — já cobria a renderização isolada do componente (número, status, datas, itens).

## Roteiro de validação manual

1. Reiniciar o frontend (`npm start`) para garantir o bundle atualizado.
2. Abrir uma nota `Open`.
3. Clicar em "Imprimir e fechar nota".
4. Na pré-visualização de impressão do navegador, conferir: nenhum toast; nenhuma barra de
   navegação; nenhum botão; somente a nota; status `Fechada`; data de fechamento preenchida; itens
   e quantidades corretos.
5. Cancelar ou concluir a impressão.
6. Confirmar que **somente depois** disso aparece um toast verde, exatamente uma vez.
7. Voltar à listagem e confirmar a nota fechada.
8. Tentar imprimir novamente: `409` tratado, nenhum diálogo de impressão, um único toast de erro.
9. Na janela de impressão, em "Mais configurações", desmarcar "Cabeçalhos e rodapés" e confirmar
   que data, título, URL e número de página desaparecem.
