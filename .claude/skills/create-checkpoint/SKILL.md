---
name: create-checkpoint
description: Revisa e cria um commit atômico de uma task concluída somente após aprovação manual explícita, sem executar push nem iniciar a próxima task.
argument-hint: '"mensagem do commit" aprovado'
arguments: [commit_message, approval]
disable-model-invocation: true
---

Crie um checkpoint usando a mensagem `$commit_message` somente se `$approval` for exatamente `aprovado` ou `approved`.

Se a aprovação estiver ausente ou tiver outro valor, pare sem alterar o Git.

1. Leia `CLAUDE.md`, `docs/progress.md` e identifique a única task em `Em validação`.
2. Confirme que o usuário aprovou explicitamente os testes manuais na conversa atual.
3. Execute `git status --short`, `git diff --check` e revise todo o diff.
4. Invoque o agente `quality-reviewer` para revisão somente leitura e aguarde o veredito.
5. Se houver achado bloqueador ou importante não resolvido, não crie o commit.
6. Execute novamente build e testes relevantes para a task.
7. Verifique que não existem segredos, `.env`, credenciais, binários ou alterações de outra task.
8. Atualize somente a task correspondente em `docs/progress.md` para `Concluída`.
9. Faça staging seletivo dos arquivos da task; não use staging indiscriminado.
10. Revise `git diff --cached --check` e `git diff --cached`.
11. Crie um único commit com `$commit_message`.
12. Não execute push e não inicie a próxima task.
13. Informe hash, mensagem, arquivos incluídos e resultados dos testes.

