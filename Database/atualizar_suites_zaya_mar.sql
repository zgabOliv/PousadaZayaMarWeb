/* ============================================================
   Zaya Mar Pousada: atualiza as suítes do banco que JÁ EXISTE no Azure
   (15 suítes: 3 para até 3 hóspedes, 6 para até 4, 5 para até 5 e 1 para 2 casais).
   ATENÇÃO: APAGA hóspedes, reservas, pagamentos e avaliações de TESTE.
   Rode conectado ao banco PousadaMareMansa, com o usuário administrador do servidor.
   ============================================================ */
BEGIN TRAN;

DELETE FROM Avaliacao;
DELETE FROM Pagamento;
DELETE FROM Reserva;
DELETE FROM LogReserva;
DELETE FROM Hospede;
DELETE FROM Quarto;
DELETE FROM TipoQuartoComodidade;
DELETE FROM Comodidade;
DELETE FROM TipoQuarto;

DBCC CHECKIDENT ('Avaliacao', RESEED, 0);
DBCC CHECKIDENT ('Pagamento', RESEED, 0);
DBCC CHECKIDENT ('Reserva', RESEED, 0);
DBCC CHECKIDENT ('LogReserva', RESEED, 0);
DBCC CHECKIDENT ('Hospede', RESEED, 0);
DBCC CHECKIDENT ('Quarto', RESEED, 0);
DBCC CHECKIDENT ('Comodidade', RESEED, 0);
DBCC CHECKIDENT ('TipoQuarto', RESEED, 0);

INSERT INTO TipoQuarto (Nome, Capacidade, PrecoDiaria, Acessivel, Descricao) VALUES
(N'Suíte Brisa',    3, 300.00, 0, N'3 suítes para até 3 hóspedes'),
(N'Suíte Coral',    4, 380.00, 0, N'6 suítes para até 4 hóspedes'),
(N'Suíte Palmeira', 5, 450.00, 0, N'5 suítes para até 5 hóspedes'),
(N'Suíte Duo',      4, 400.00, 0, N'1 suíte para até 2 casais');

-- 15 suítes: 3 Brisa, 6 Coral, 5 Palmeira e 1 Duo
INSERT INTO Quarto (Numero, IdTipoQuarto) VALUES
('101',1),('102',1),('103',1),
('104',2),('105',2),('106',2),('107',2),('108',2),('109',2),
('201',3),('202',3),('203',3),('204',3),('205',3),
('206',4);

INSERT INTO Comodidade (Nome) VALUES (N'Wi-Fi'),(N'Café da manhã'),(N'Estacionamento');

INSERT INTO TipoQuartoComodidade VALUES
(1,1),(1,2),(1,3),(2,1),(2,2),(2,3),(3,1),(3,2),(3,3),(4,1),(4,2),(4,3);

COMMIT;

-- Conferência: deve mostrar 4 tipos e 15 suítes no total
SELECT t.Nome, t.Capacidade, t.PrecoDiaria, COUNT(q.IdQuarto) AS Suites
FROM TipoQuarto t LEFT JOIN Quarto q ON q.IdTipoQuarto = t.IdTipoQuarto
GROUP BY t.Nome, t.Capacidade, t.PrecoDiaria ORDER BY t.PrecoDiaria;
