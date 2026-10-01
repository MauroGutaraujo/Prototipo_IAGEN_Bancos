from load import a_odbc


def test_convierte_cadena_ado_net_a_odbc():
    odbc = a_odbc("Server=localhost,1433;Database=ReclamosTesis;User Id=sa;Password=x;y;TrustServerCertificate=True")
    assert odbc.startswith("DRIVER={ODBC Driver 18 for SQL Server}")
    assert "SERVER=localhost,1433" in odbc and "DATABASE=ReclamosTesis" in odbc
    assert "UID=sa" in odbc and "TrustServerCertificate=yes" in odbc


def test_respeta_cadena_odbc():
    assert a_odbc("DRIVER={X};SERVER=s") == "DRIVER={X};SERVER=s"
