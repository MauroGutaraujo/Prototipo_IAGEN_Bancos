---
trigger: always_on
description: "Manejo de secretos y datos."
---
# Seguridad y datos

- Claves de API y cadenas de conexión solo en variables de entorno o `dotnet user-secrets`. `.env` está en `.gitignore`.
- Nunca imprimas claves en logs ni en la consola.
- Solo datos sintéticos. Si detectas algo que parece un dato real (DNI, número de tarjeta, nombre real), detente y avisa.
- El usuario de BD de la API no tiene permisos sobre el esquema `eval`.
