# Capturas de tela do sistema

Esta pasta guarda as imagens referenciadas em `README.md` (seção "Visão do sistema") e em
`docs/screenshots.md` (galeria completa). Nenhuma imagem foi gravada automaticamente aqui — o
assistente que redigiu esta documentação (Task 16) não tinha uma ferramenta capaz de extrair os
bytes de imagens coladas na conversa e salvá-las como arquivo; apenas os nomes e o destino de cada
arquivo foram definidos.

## Convenção de nomes

Inglês, minúsculo, com hífen, extensão `.png`.

## Arquivos esperados

| Arquivo | Tela | Usado em |
| --- | --- | --- |
| `products-list.png` | Listagem de produtos, paginada | README — galeria principal |
| `product-form-dialog.png` | Modal "Cadastrar produto", campos vazios | README — galeria principal |
| `invoices-list.png` | Listagem de notas fiscais, paginada | README — galeria principal |
| `invoice-form-dialog.png` | Modal "Nova nota fiscal", com itens preenchidos | README — galeria principal |
| `invoice-detail.png` | Detalhe da nota, status `FECHADA` após impressão | README — galeria principal |
| `invoice-print-preview.png` | Visualização preparada para impressão (`invoice-print-view`) | **Pendente** — não fornecida ainda |
| `product-form-dialog-validation.png` | Modal de cadastro de produto com erros de validação inline | `docs/screenshots.md` |
| `product-create-success.png` | Listagem de produtos com toast de sucesso após cadastro | `docs/screenshots.md` |
| `invoice-form-dialog-empty.png` | Modal "Nova nota fiscal" recém-aberto, sem itens | `docs/screenshots.md` |
| `invoice-form-dialog-single-item.png` | Modal "Nova nota fiscal" com um item adicionado | `docs/screenshots.md` |
| `invoice-detail-open.png` | Detalhe da nota, status `ABERTA`, antes da impressão | `docs/screenshots.md` |
| `invoice-detail-insufficient-stock.png` | Detalhe da nota mostrando erro de saldo insuficiente ao tentar imprimir | `docs/screenshots.md` |

`invoice-print-preview.png` não estava entre as capturas fornecidas na Task 16 — a tela
`invoice-print-view` (visualização com CSS `@media print`) precisa de uma captura dedicada para
preencher essa posição da galeria principal.
