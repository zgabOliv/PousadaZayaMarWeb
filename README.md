# Zaya Mar Pousada: site em ASP.NET Core MVC (PIM IV)

Requisitos: .NET 8 SDK e SQL Server 2019+.

## Passo a passo
1. No SQL Server Management Studio, abra `Database/00_criar_banco_completo.sql` e execute tudo (F5). Ele cria banco, procedures e os usuários app_site e app_admin.
2. As senhas padrão já estão no `appsettings.json`. Se o SQL Server só aceitar login do Windows, use `Server=localhost;Database=PousadaMareMansa;Trusted_Connection=True;TrustServerCertificate=True` nas duas connection strings.
3. (Opcional) Coloque as connection strings (sem versionar senhas):
   `dotnet user-secrets init`
   `dotnet user-secrets set "ConnectionStrings:Site" "Server=...;Database=PousadaMareMansa;User Id=app_site;Password=...;TrustServerCertificate=True"`
   `dotnet user-secrets set "ConnectionStrings:Admin" "Server=...;Database=PousadaMareMansa;User Id=app_admin;Password=...;TrustServerCertificate=True"`
4. Crie o primeiro administrador: `dotnet run -- --criar-admin gerente SuaSenhaForte1 Gerente`
5. Execute: `dotnet run` e abra o endereço mostrado no terminal.

## Arquitetura (Etapa 6)
- **Models**: classes de dados e validação (DataAnnotations).
- **Data**: `Db` (conexões), `PousadaRepository` e `AdminRepository`. Só chamam stored procedures.
- **Controllers**: Home, Reserva e Admin. Recebem a requisição, validam e chamam o repositório.
- **Views**: Razor (.cshtml) com layout compartilhado.
- **Services**: `PasswordHasher` (PBKDF2-SHA256 com salt).

## Segurança aplicada
- Conexão do site no papel `rl_site` (só executa procedures, sem acesso às tabelas).
- Consultas parametrizadas (sem concatenar SQL), token anti-CSRF em todos os POST.
- Senhas de administrador com hash e salt, cookie HttpOnly, mesma mensagem para usuário ou senha inválidos.
- Cancelamento pelo hóspede exige código e e-mail da reserva.

## API REST (Etapa 4)
Com o site rodando, a documentação interativa fica em `http://localhost:5000/swagger`.
O arquivo `pousada-api.http` tem exemplos prontos de chamadas.

| Método | Rota | Acesso | Função |
|---|---|---|---|
| GET | /api/v1/quartos | público | Lista tipos de quarto |
| GET | /api/v1/quartos/disponibilidade | público | Quartos livres e valor total do período |
| POST | /api/v1/reservas | público | Cria reserva (201 com o código) |
| POST | /api/v1/reservas/consulta | público | Consulta por código e e-mail |
| POST | /api/v1/reservas/cancelamento | público | Cancela (código e e-mail) |
| POST | /api/v1/contato | público | Registra mensagem |
| POST | /api/v1/auth/login | público | Login admin, devolve JWT (5 tentativas/min) |
| GET | /api/v1/admin/reservas | JWT Gerente/Recepcao | Lista reservas |
| POST | /api/v1/admin/reservas/{codigo}/pagamentos | JWT Gerente/Recepcao | Registra pagamento |
| POST | /api/v1/admin/reservas/{codigo}/cancelamento | JWT Gerente | Cancela reserva |

Erros seguem o padrão ProblemDetails: 400 (validação ou regra), 401/403 (autenticação/perfil),
404 (reserva inexistente), 409 (sem quarto livre) e 429 (excesso de tentativas de login).
Em produção, troque `Jwt:Key` (mínimo 32 caracteres) via `user-secrets` ou variável de ambiente.

## App mobile (Etapa 5)
PWA instalável em `/app/` (link "App mobile" no menu). Usa só a API REST, guarda dados no aparelho
(quartos, reservas salvas e fila de mensagens offline) e funciona sem conexão para consulta.
Para ver como app: abra `http://localhost:5000/app/` no Chrome do computador, tecle F12, ative o modo celular e, na aba Application, confira o Service Worker e teste o modo Offline. A instalação na tela inicial e o uso offline exigem HTTPS ou localhost.
Para abrir no celular pela rede local, use o perfil "Rede (testar no celular)" do Visual Studio, libere a porta 5000 no firewall do Windows e acesse `http://<ip-do-computador>:5000/app/` (funciona como site, sem instalar).

## Containers, pipeline e monitoramento (Etapa 8)
- `docker compose up --build`: sobe SQL Server, cria o banco e inicia o site em `http://localhost:8080`.
- `.github/workflows/ci-cd.yml`: build, publicação, imagem Docker no GHCR e deploy no Azure App Service.
- `/health` (processo no ar) e `/health/ready` (processo + banco) para monitoramento.
- Obs.: com `.sln` e `.csproj` na mesma pasta, `dotnet build` pede o arquivo: `dotnet build PousadaMareMansa.Web.csproj`.

## Versão 2, parte 1: contas de hóspede, suítes e avaliações

**Antes de publicar este site, rode `Database/01_v2_parte1_contas_avaliacoes.sql` uma vez no banco (SSMS, como administrador).**
Sem isso a página inicial e a API de quartos dão erro, porque as procedures `sp_ListarTiposQuarto` etc. ganharam colunas novas.

Novidades:
- Contas: `/Conta/Cadastro`, `/Conta/Entrar`, `/Conta/Esqueci`, `/Conta/MinhasReservas`, avaliação em `/Conta/Avaliar`.
  Cookie próprio (`zm.hospede`); o painel administrativo continua com outro cookie e o hóspede não acessa `/Admin`.
  Senha com PBKDF2, bloqueio de 15 min após 5 erros, links de e-mail com token aleatório (só o hash vai ao banco).
- Suítes: `/Suites` e `/Suites/Detalhe/{id}` com calendário de disponibilidade, comodidades, nota média e comentários. `/Home/Regras`.
- API: `POST /api/v1/conta/cadastro|login|esqueci-senha`, `GET /api/v1/conta/me|reservas`, `POST /api/v1/conta/reservas/{codigo}/cancelar|avaliacao`,
  `GET /api/v1/quartos/{id}`, `/quartos/{id}/calendario?mes=yyyy-MM`, `/quartos/avaliacoes`.
- E-mails: toda mensagem é gravada na tabela `EmailSaida`. Para enviar de verdade, configure `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__From`.
  Sem SMTP, para demonstrar o fluxo, defina `Email__MostrarLinks=true` (o link aparece na tela; já vem ligado em Development). Não deixe ligado em produção real.
- Fotos das suítes: não há fotos reais. Coloque os arquivos em `wwwroot/img/suites/` e rode, por exemplo:
  `UPDATE TipoQuarto SET Imagem='/img/suites/brisa.jpg' WHERE Nome='Brisa';` (sem foto, o site mostra a logo).

## Versão 2, parte 2: operação da equipe

Rode também `Database/02_v2_parte2_operacao.sql` (depois do `01_...`), uma vez, no mesmo banco. Ele é reexecutável.

Painel `/Admin` (cada perfil vê só o que pode):
- **Recepção**: Hoje (entradas, saídas, check-in/check-out, receber saldo), Reservas (buscar, filtrar, nova reserva por balcão/telefone com pagamento na hora, pagar, check-in, check-out), Mapa de ocupação por dia, Hóspedes, Mensagens (marcar como respondida).
- **Gerente**: tudo isso, mais cancelar reservas, Suítes e tarifas (criar/editar tipo, diária, comodidades, foto, quartos ativos), Relatórios (ocupação por suíte e receita por mês) e Usuários (criar, trocar perfil, desativar, trocar senha; nunca fica sem gerente ativo).
- Regras no banco: check-in só com reserva confirmada e a partir da data de entrada; check-out só após o check-in e com saldo quitado (a reserva vira "Concluida"); não dá para desativar quarto com reservas futuras.

API (JWT, perfis Gerente/Recepcao): `GET /api/v1/admin/painel|movimento|mapa|hospedes|mensagens`, `POST /api/v1/admin/reservas` (reserva da recepção),
`POST /api/v1/admin/reservas/{codigo}/checkin|checkout`, `GET /api/v1/admin/relatorios/ocupacao|receita` (só Gerente).

Ordem para publicar: (1) rodar os dois scripts no banco, (2) publicar o site, (3) gerar o APK com `gerar-apk.bat`.

Fora desta versão (previstos no PIM como evolução futura): bloqueio da suíte por 10 minutos durante o pagamento, Checkout Pro do Mercado Pago e integração com o Booking.com.
