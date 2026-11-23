/* ============================================================
   Zaya Mar Pousada - versão 2, parte 2: operação da equipe
   Rode UMA vez no banco (SSMS), depois da parte 1. Pode rodar de novo (CREATE OR ALTER).
   Inclui: check-in/check-out, mapa de ocupação, movimento do dia, reserva feita pela
   recepção (RF22), CRUD de suítes e tarifas, hóspedes, mensagens, usuários e relatórios.
   ============================================================ */

IF COL_LENGTH('Reserva', 'CheckInEm') IS NULL
    ALTER TABLE Reserva ADD CheckInEm DATETIME2(0) NULL, CheckOutEm DATETIME2(0) NULL;
GO

-- A view ganha as colunas novas no FIM (o código existente lê por posição)
CREATE OR ALTER VIEW vw_ReservasDetalhadas AS
SELECT r.IdReserva, r.Codigo, h.Nome AS Hospede, q.Numero AS Quarto, t.Nome AS Tipo,
       r.DataEntrada, r.DataSaida, DATEDIFF(DAY, r.DataEntrada, r.DataSaida) AS Noites,
       r.QtdHospedes, r.ValorTotal, r.Status,
       ISNULL((SELECT SUM(p.Valor) FROM Pagamento p
               WHERE p.IdReserva = r.IdReserva AND p.Status = 'Aprovado'), 0) AS ValorPago,
       r.CheckInEm, r.CheckOutEm
FROM Reserva r
JOIN Hospede h   ON h.IdHospede = r.IdHospede
JOIN Quarto q    ON q.IdQuarto = r.IdQuarto
JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto;
GO

/* ---------- pagamento: nunca aceita mais do que o saldo da reserva ---------- */
CREATE OR ALTER PROCEDURE sp_RegistrarPagamento
    @Codigo VARCHAR(10), @Valor DECIMAL(10,2), @Forma VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT, @Status VARCHAR(12), @Total DECIMAL(10,2), @Pago DECIMAL(10,2);
    SELECT @Id = IdReserva, @Status = Status, @Total = ValorTotal FROM Reserva WHERE Codigo = @Codigo;
    IF @Id IS NULL THROW 50006, 'Reserva não encontrada.', 1;
    IF @Status = 'Cancelada' THROW 50008, 'Não é possível pagar uma reserva cancelada.', 1;
    IF @Valor <= 0 THROW 50009, 'O valor do pagamento deve ser maior que zero.', 1;
    SELECT @Pago = ISNULL(SUM(Valor), 0) FROM Pagamento WHERE IdReserva = @Id AND Status = 'Aprovado';
    IF @Pago + @Valor > @Total
        THROW 50012, 'O valor ultrapassa o saldo da reserva. Confira o valor digitado.', 1;
    INSERT INTO Pagamento (IdReserva, Valor, Forma) VALUES (@Id, @Valor, @Forma);
END;
GO

/* ---------- reserva feita pela recepção (RF22), com pagamento opcional ---------- */
CREATE OR ALTER PROCEDURE sp_AdminCriarReserva
    @Nome NVARCHAR(120), @Email NVARCHAR(160), @Telefone VARCHAR(20),
    @IdTipoQuarto INT, @Entrada DATE, @Saida DATE, @Hospedes TINYINT,
    @ValorPago DECIMAL(10,2) = NULL, @Forma VARCHAR(10) = NULL,
    @Codigo VARCHAR(10) OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRAN;
        EXEC sp_CriarReserva @Nome, @Email, @Telefone, @IdTipoQuarto, @Entrada, @Saida, @Hospedes, @Codigo OUTPUT;
        IF @ValorPago IS NOT NULL AND @ValorPago > 0
        BEGIN
            IF @Forma NOT IN ('Pix','Cartao','Dinheiro') OR @Forma IS NULL THROW 50032, 'Informe a forma de pagamento (Pix, Cartao ou Dinheiro).', 1;
            EXEC sp_RegistrarPagamento @Codigo, @ValorPago, @Forma;
        END
    COMMIT;
END;
GO

/* ---------- check-in e check-out ---------- */
CREATE OR ALTER PROCEDURE sp_CheckIn @Codigo VARCHAR(10) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT, @Status VARCHAR(12), @Ent DATE, @Sai DATE, @In DATETIME2(0);
    DECLARE @Hoje DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'E. South America Standard Time' AS DATE);
    SELECT @Id = IdReserva, @Status = Status, @Ent = DataEntrada, @Sai = DataSaida, @In = CheckInEm FROM Reserva WHERE Codigo = @Codigo;
    IF @Id IS NULL THROW 50006, 'Reserva não encontrada.', 1;
    IF @In IS NOT NULL THROW 50033, 'O check-in desta reserva já foi feito.', 1;
    IF @Status <> 'Confirmada' THROW 50030, 'O check-in exige reserva confirmada (pagamento total registrado).', 1;
    IF @Hoje < @Ent THROW 50031, 'O check-in só pode ser feito a partir do dia da entrada.', 1;
    IF @Hoje >= @Sai THROW 50031, 'O período desta reserva já terminou.', 1;
    UPDATE Reserva SET CheckInEm = SYSDATETIME() WHERE IdReserva = @Id;
END;
GO

CREATE OR ALTER PROCEDURE sp_CheckOut @Codigo VARCHAR(10) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT, @Total DECIMAL(10,2), @In DATETIME2(0), @Out DATETIME2(0), @Pago DECIMAL(10,2);
    SELECT @Id = IdReserva, @Total = ValorTotal, @In = CheckInEm, @Out = CheckOutEm FROM Reserva WHERE Codigo = @Codigo;
    IF @Id IS NULL THROW 50006, 'Reserva não encontrada.', 1;
    IF @In IS NULL THROW 50034, 'Faça o check-in antes do check-out.', 1;
    IF @Out IS NOT NULL THROW 50033, 'O check-out desta reserva já foi feito.', 1;
    SELECT @Pago = ISNULL(SUM(Valor), 0) FROM Pagamento WHERE IdReserva = @Id AND Status = 'Aprovado';
    IF @Pago < @Total THROW 50035, 'Há saldo a pagar. Registre o pagamento antes do check-out.', 1;
    UPDATE Reserva SET CheckOutEm = SYSDATETIME(), Status = 'Concluida' WHERE IdReserva = @Id;
END;
GO

/* ---------- mapa de ocupação e movimento do dia ---------- */
CREATE OR ALTER PROCEDURE sp_MapaOcupacao @Data DATE AS
BEGIN
    SET NOCOUNT ON;
    SELECT q.Numero AS Quarto, t.Nome AS Tipo, t.Capacidade,
           CASE WHEN r.IdReserva IS NULL THEN 'Livre'
                WHEN r.CheckInEm IS NOT NULL AND r.CheckOutEm IS NULL THEN 'Hospedado'
                WHEN r.CheckOutEm IS NOT NULL THEN 'Saiu'
                WHEN r.Status = 'Pendente' THEN 'Pendente'
                ELSE 'Reservado' END AS Situacao,
           r.Codigo, h.Nome AS Hospede, r.DataEntrada, r.DataSaida
    FROM Quarto q
    JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto
    LEFT JOIN Reserva r ON r.IdQuarto = q.IdQuarto AND r.Status <> 'Cancelada'
         AND r.DataEntrada <= @Data AND r.DataSaida > @Data
    LEFT JOIN Hospede h ON h.IdHospede = r.IdHospede
    WHERE q.Ativo = 1
    ORDER BY q.Numero;
END;
GO

CREATE OR ALTER PROCEDURE sp_MovimentoDia @Data DATE AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Entrada' AS Movimento, r.Codigo, h.Nome AS Hospede, q.Numero AS Quarto, t.Nome AS Tipo,
           r.DataEntrada, r.DataSaida, r.Status, r.ValorTotal,
           ISNULL((SELECT SUM(p.Valor) FROM Pagamento p WHERE p.IdReserva = r.IdReserva AND p.Status = 'Aprovado'), 0) AS ValorPago,
           r.CheckInEm, r.CheckOutEm
    FROM Reserva r JOIN Hospede h ON h.IdHospede = r.IdHospede JOIN Quarto q ON q.IdQuarto = r.IdQuarto JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto
    WHERE r.DataEntrada = @Data AND r.Status <> 'Cancelada'
    UNION ALL
    SELECT 'Saida', r.Codigo, h.Nome, q.Numero, t.Nome, r.DataEntrada, r.DataSaida, r.Status, r.ValorTotal,
           ISNULL((SELECT SUM(p.Valor) FROM Pagamento p WHERE p.IdReserva = r.IdReserva AND p.Status = 'Aprovado'), 0),
           r.CheckInEm, r.CheckOutEm
    FROM Reserva r JOIN Hospede h ON h.IdHospede = r.IdHospede JOIN Quarto q ON q.IdQuarto = r.IdQuarto JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto
    WHERE r.DataSaida = @Data AND r.Status <> 'Cancelada'
    ORDER BY 1, 4;
END;
GO

CREATE OR ALTER PROCEDURE sp_PainelResumo AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoje DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'E. South America Standard Time' AS DATE);
    DECLARE @IniMes DATE = DATEFROMPARTS(YEAR(@Hoje), MONTH(@Hoje), 1);
    SELECT @Hoje AS Hoje,
        (SELECT COUNT(*) FROM Quarto WHERE Ativo = 1) AS TotalQuartos,
        (SELECT COUNT(*) FROM Reserva WHERE Status <> 'Cancelada' AND DataEntrada <= @Hoje AND DataSaida > @Hoje) AS QuartosOcupadosHoje,
        (SELECT COUNT(*) FROM Reserva WHERE Status <> 'Cancelada' AND DataEntrada = @Hoje) AS EntradasHoje,
        (SELECT COUNT(*) FROM Reserva WHERE Status <> 'Cancelada' AND DataEntrada = @Hoje AND CheckInEm IS NULL) AS CheckInsPendentes,
        (SELECT COUNT(*) FROM Reserva WHERE Status <> 'Cancelada' AND DataSaida = @Hoje) AS SaidasHoje,
        (SELECT COUNT(*) FROM Reserva WHERE CheckInEm IS NOT NULL AND CheckOutEm IS NULL) AS Hospedados,
        (SELECT COUNT(*) FROM Reserva WHERE Status = 'Pendente') AS ReservasPendentes,
        (SELECT COUNT(*) FROM MensagemContato WHERE Respondida = 0) AS MensagensNovas,
        ISNULL((SELECT SUM(Valor) FROM Pagamento WHERE Status = 'Aprovado' AND CAST(DataPagamento AS DATE) >= @IniMes), 0) AS ReceitaMes;
END;
GO

/* ---------- suítes, quartos e tarifas ---------- */
CREATE OR ALTER PROCEDURE sp_AdminListarQuartos AS
BEGIN
    SET NOCOUNT ON;
    SELECT q.IdQuarto, q.Numero, q.Ativo, t.IdTipoQuarto, t.Nome AS Tipo
    FROM Quarto q JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto ORDER BY q.Numero;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminSalvarTipo
    @IdTipoQuarto INT = NULL, @Nome NVARCHAR(60), @Capacidade TINYINT, @PrecoDiaria DECIMAL(10,2),
    @Acessivel BIT, @Descricao NVARCHAR(300) = NULL, @Imagem NVARCHAR(200) = NULL, @Comodidades NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF LEN(LTRIM(RTRIM(@Nome))) < 2 THROW 50040, 'Informe o nome da suíte.', 1;
    IF @Capacidade NOT BETWEEN 1 AND 8 THROW 50041, 'A capacidade deve ser de 1 a 8.', 1;
    IF @PrecoDiaria <= 0 THROW 50042, 'A diária deve ser maior que zero.', 1;
    IF EXISTS (SELECT 1 FROM TipoQuarto WHERE Nome = @Nome AND (@IdTipoQuarto IS NULL OR IdTipoQuarto <> @IdTipoQuarto))
        THROW 50043, 'Já existe uma suíte com esse nome.', 1;
    BEGIN TRAN;
        IF @IdTipoQuarto IS NULL
        BEGIN
            INSERT INTO TipoQuarto (Nome, Capacidade, PrecoDiaria, Acessivel, Descricao, Imagem)
            VALUES (LTRIM(RTRIM(@Nome)), @Capacidade, @PrecoDiaria, @Acessivel, NULLIF(@Descricao, ''), NULLIF(@Imagem, ''));
            SET @IdTipoQuarto = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            -- A nova diária vale só para reservas novas (o valor das já feitas fica gravado em Reserva.ValorTotal)
            UPDATE TipoQuarto SET Nome = LTRIM(RTRIM(@Nome)), Capacidade = @Capacidade, PrecoDiaria = @PrecoDiaria,
                   Acessivel = @Acessivel, Descricao = NULLIF(@Descricao, ''), Imagem = NULLIF(@Imagem, '')
            WHERE IdTipoQuarto = @IdTipoQuarto;
            IF @@ROWCOUNT = 0 THROW 50006, 'Suíte não encontrada.', 1;
        END
        IF @Comodidades IS NOT NULL
        BEGIN
            DECLARE @Lista TABLE (Nome NVARCHAR(80) PRIMARY KEY);
            INSERT INTO @Lista SELECT DISTINCT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Comodidades, ',') WHERE LTRIM(RTRIM(value)) <> '';
            INSERT INTO Comodidade (Nome) SELECT l.Nome FROM @Lista l WHERE NOT EXISTS (SELECT 1 FROM Comodidade c WHERE c.Nome = l.Nome);
            DELETE FROM TipoQuartoComodidade WHERE IdTipoQuarto = @IdTipoQuarto;
            INSERT INTO TipoQuartoComodidade (IdTipoQuarto, IdComodidade)
            SELECT @IdTipoQuarto, c.IdComodidade FROM Comodidade c JOIN @Lista l ON l.Nome = c.Nome;
        END
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminSalvarQuarto
    @IdQuarto INT = NULL, @Numero VARCHAR(10), @IdTipoQuarto INT, @Ativo BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF LEN(LTRIM(RTRIM(@Numero))) = 0 THROW 50044, 'Informe o número do quarto.', 1;
    IF NOT EXISTS (SELECT 1 FROM TipoQuarto WHERE IdTipoQuarto = @IdTipoQuarto) THROW 50003, 'Tipo de quarto inexistente.', 1;
    IF EXISTS (SELECT 1 FROM Quarto WHERE Numero = @Numero AND (@IdQuarto IS NULL OR IdQuarto <> @IdQuarto))
        THROW 50045, 'Já existe um quarto com esse número.', 1;
    IF @IdQuarto IS NULL
        INSERT INTO Quarto (Numero, IdTipoQuarto, Ativo) VALUES (LTRIM(RTRIM(@Numero)), @IdTipoQuarto, @Ativo);
    ELSE
    BEGIN
        IF @Ativo = 0 AND EXISTS (SELECT 1 FROM Reserva WHERE IdQuarto = @IdQuarto AND Status <> 'Cancelada'
                                  AND DataSaida > CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'E. South America Standard Time' AS DATE))
            THROW 50046, 'Este quarto tem reservas em andamento ou futuras. Cancele ou mude as reservas antes de desativá-lo.', 1;
        UPDATE Quarto SET Numero = LTRIM(RTRIM(@Numero)), IdTipoQuarto = @IdTipoQuarto, Ativo = @Ativo WHERE IdQuarto = @IdQuarto;
        IF @@ROWCOUNT = 0 THROW 50006, 'Quarto não encontrado.', 1;
    END
END;
GO

/* ---------- hóspedes e mensagens ---------- */
CREATE OR ALTER PROCEDURE sp_AdminListarHospedes
WITH EXECUTE AS OWNER   -- a equipe precisa ver telefone e e-mail sem máscara para atender
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.IdHospede, h.Nome, h.Email, h.Telefone, h.DataCadastro,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM ContaHospede c WHERE c.IdHospede = h.IdHospede) THEN 1 ELSE 0 END AS BIT) AS TemConta,
           (SELECT COUNT(*) FROM Reserva r WHERE r.IdHospede = h.IdHospede AND r.Status <> 'Cancelada') AS Reservas,
           (SELECT MAX(r.DataEntrada) FROM Reserva r WHERE r.IdHospede = h.IdHospede AND r.Status <> 'Cancelada') AS UltimaEstadia
    FROM Hospede h WHERE h.Ativo = 1 ORDER BY h.Nome;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminListarMensagens AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdMensagem, Nome, Email, Texto, DataEnvio, Respondida FROM MensagemContato ORDER BY Respondida, DataEnvio DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminMarcarMensagem @IdMensagem INT, @Respondida BIT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE MensagemContato SET Respondida = @Respondida WHERE IdMensagem = @IdMensagem;
END;
GO

/* ---------- usuários da equipe ---------- */
CREATE OR ALTER PROCEDURE sp_AdminListarUsuarios AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdUsuario, Login, Perfil, Ativo FROM UsuarioAdmin ORDER BY Ativo DESC, Login;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminAlterarUsuario @IdUsuario INT, @Perfil VARCHAR(15), @Ativo BIT AS
BEGIN
    SET NOCOUNT ON;
    IF @Perfil NOT IN ('Gerente','Recepcao') THROW 50050, 'Perfil inválido.', 1;
    IF NOT EXISTS (SELECT 1 FROM UsuarioAdmin WHERE IdUsuario = @IdUsuario) THROW 50006, 'Usuário não encontrado.', 1;
    -- nunca deixar o sistema sem gerente ativo
    IF NOT EXISTS (SELECT 1 FROM UsuarioAdmin WHERE IdUsuario <> @IdUsuario AND Perfil = 'Gerente' AND Ativo = 1)
       AND (@Perfil <> 'Gerente' OR @Ativo = 0)
        THROW 50051, 'É preciso manter ao menos um gerente ativo.', 1;
    UPDATE UsuarioAdmin SET Perfil = @Perfil, Ativo = @Ativo WHERE IdUsuario = @IdUsuario;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminRedefinirSenhaUsuario @IdUsuario INT, @Hash VARBINARY(64), @Salt VARBINARY(32) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE UsuarioAdmin SET SenhaHash = @Hash, Salt = @Salt WHERE IdUsuario = @IdUsuario;
    IF @@ROWCOUNT = 0 THROW 50006, 'Usuário não encontrado.', 1;
END;
GO

/* ---------- relatórios (gerente) ---------- */
CREATE OR ALTER PROCEDURE sp_RelatorioPeriodo @Inicio DATE, @Fim DATE AS
BEGIN
    SET NOCOUNT ON;
    IF @Fim <= @Inicio THROW 50060, 'A data final deve ser posterior à inicial.', 1;
    IF DATEDIFF(DAY, @Inicio, @Fim) > 366 THROW 50061, 'O período máximo é de 366 dias.', 1;
    DECLARE @Dias INT = DATEDIFF(DAY, @Inicio, @Fim);
    SELECT t.Nome AS Tipo,
           (SELECT COUNT(*) FROM Quarto q0 WHERE q0.IdTipoQuarto = t.IdTipoQuarto AND q0.Ativo = 1) AS Unidades,
           COUNT(r.IdReserva) AS Reservas,
           ISNULL(SUM(DATEDIFF(DAY,
               CASE WHEN r.DataEntrada < @Inicio THEN @Inicio ELSE r.DataEntrada END,
               CASE WHEN r.DataSaida   > @Fim    THEN @Fim    ELSE r.DataSaida   END)), 0) AS DiariasOcupadas,
           @Dias * (SELECT COUNT(*) FROM Quarto q0 WHERE q0.IdTipoQuarto = t.IdTipoQuarto AND q0.Ativo = 1) AS DiariasDisponiveis,
           ISNULL(SUM(r.ValorTotal), 0) AS ValorReservado
    FROM TipoQuarto t
    LEFT JOIN Quarto q ON q.IdTipoQuarto = t.IdTipoQuarto
    LEFT JOIN Reserva r ON r.IdQuarto = q.IdQuarto AND r.Status <> 'Cancelada'
         AND r.DataEntrada < @Fim AND r.DataSaida > @Inicio
    GROUP BY t.IdTipoQuarto, t.Nome
    ORDER BY t.Nome;
END;
GO

CREATE OR ALTER PROCEDURE sp_ReceitaMensal AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (12) Ano, Mes, Receita, Reservas FROM vw_ReceitaMensal ORDER BY Ano DESC, Mes DESC;
END;
GO

/* ---------- permissões (o site público NÃO recebe nenhuma destas) ---------- */
GRANT EXECUTE ON sp_AdminCriarReserva TO rl_gerente;
GRANT EXECUTE ON sp_CheckIn TO rl_gerente;
GRANT EXECUTE ON sp_CheckOut TO rl_gerente;
GRANT EXECUTE ON sp_MapaOcupacao TO rl_gerente;
GRANT EXECUTE ON sp_MovimentoDia TO rl_gerente;
GRANT EXECUTE ON sp_PainelResumo TO rl_gerente;
GRANT EXECUTE ON sp_AdminListarQuartos TO rl_gerente;
GRANT EXECUTE ON sp_AdminSalvarTipo TO rl_gerente;
GRANT EXECUTE ON sp_AdminSalvarQuarto TO rl_gerente;
GRANT EXECUTE ON sp_AdminListarHospedes TO rl_gerente;
GRANT EXECUTE ON sp_AdminListarMensagens TO rl_gerente;
GRANT EXECUTE ON sp_AdminMarcarMensagem TO rl_gerente;
GRANT EXECUTE ON sp_AdminListarUsuarios TO rl_gerente;
GRANT EXECUTE ON sp_AdminAlterarUsuario TO rl_gerente;
GRANT EXECUTE ON sp_AdminRedefinirSenhaUsuario TO rl_gerente;
GRANT EXECUTE ON sp_RelatorioPeriodo TO rl_gerente;
GRANT EXECUTE ON sp_ReceitaMensal TO rl_gerente;
GRANT EXECUTE ON sp_ListarTiposQuarto TO rl_gerente;
GRANT EXECUTE ON sp_CriarReserva TO rl_gerente;
GRANT EXECUTE ON sp_RegistrarPagamento TO rl_gerente;
GRANT SELECT ON vw_ReceitaMensal TO rl_gerente;
GRANT SELECT ON vw_ReservasDetalhadas TO rl_gerente;
GO
