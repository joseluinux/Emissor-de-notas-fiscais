-- Executado apenas na PRIMEIRA subida, quando o volume de dados esta vazio.
-- Cada servico e dono do seu proprio banco: o Faturamento nunca le as tabelas do Estoque,
-- so a API HTTP dele.
CREATE DATABASE estoque;
CREATE DATABASE faturamento;
