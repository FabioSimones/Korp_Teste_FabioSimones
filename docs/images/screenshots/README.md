# Capturas de tela do sistema

Catálogo dos arquivos que realmente existem nesta pasta, usados em `README.md` (seção "Visão do
sistema") e em `docs/screenshots.md` (galeria completa). Convenção de nomes: inglês, minúsculo,
com hífen, extensão real do arquivo (todas atualmente `.png`).

## Arquivos existentes

| Arquivo | Tela | Finalidade | README principal | Galeria completa |
| --- | --- | --- | :---: | :---: |
| `products-list.png` | Listagem de produtos, paginada | Consulta de produtos | ✅ | ✅ |
| `product-form-dialog.png` | Modal "Cadastrar produto", campos vazios | Cadastro de produto por modal | ✅ | ✅ |
| `product-form-dialog-validation.png` | Modal de cadastro de produto com erros de validação inline | Validação client-side (código/descrição/saldo) | — | ✅ |
| `product-create-success.png` | Listagem de produtos + toast de sucesso | Confirmação de cadastro (`201`) | — | ✅ |
| `products-list-service-unavailable.png` | Tela de produtos com erro "serviço de estoque indisponível" | Estado de erro quando `Inventory.Api` está fora do ar | — | ✅ |
| `invoices-list.png` | Listagem de notas fiscais, paginada | Consulta de notas fiscais | ✅ | ✅ |
| `invoice-form-dialog.png` | Modal "Nova nota fiscal", 2 itens preenchidos, botão habilitado | Criação de nota por modal | ✅ | ✅ |
| `invoice-detail-open.png` | Detalhe da nota, status `ABERTA` | Nota aguardando impressão/fechamento | — | ✅ |
| `invoice-detail-insufficient-stock.png` | Detalhe da nota `ABERTA`, toast de saldo insuficiente | Impressão rejeitada por saldo insuficiente (`409`) | — | ✅ |
| `invoice-detail-closed.png` | Detalhe da nota, status `FECHADA` | Detalhes, status e fechamento da nota | ✅ | ✅ |
| `invoices-list-service-unavailable.png` | Tela de notas fiscais com erro "serviço de estoque indisponível" | Estado de erro quando `Billing.Api` está fora do ar | — | ✅ |
| `invoice-print-preview.png` | Diálogo de impressão do navegador, `Status: Fechada`, sem toast/navegação | Visualização preparada para impressão (pós-correção da Task 17) | ✅ | ✅ |
| `swagger-billing-api.png` | Swagger UI — `Billing.Api v1` | Documentação interativa dos endpoints de notas | — | ✅ |
| `swagger-inventory-api.png` | Swagger UI — `Inventory.Api v1` | Documentação interativa dos endpoints de produtos/estoque | — | ✅ |

Todos os 14 arquivos foram inspecionados visualmente: nenhum contém senha, connection string, User
Secret, conteúdo de `.env`, caminho pessoal do Windows, e-mail, DevTools, terminal, stack trace ou
dado real de cliente. Nenhuma duplicata binária (hashes SHA-256 únicos).

## Capturas pendentes

Nenhuma no momento — as 6 posições da galeria principal do README e todas as categorias previstas
para a galeria completa estão preenchidas com imagens reais e verificadas.
