// Ficha de cronometraje T0 (SPEC §5). Los tiempos los mide el servidor; el navegador solo envía eventos.
const $ = (id) => document.getElementById(id);
let casoActual = null;
let latido = null;

async function api(metodo, ruta, cuerpo) {
  const r = await fetch(`/ficha/api${ruta}`, {
    method: metodo,
    headers: cuerpo ? { "Content-Type": "application/json" } : {},
    body: cuerpo ? JSON.stringify(cuerpo) : undefined,
  });
  if (r.status === 401) { mostrar("vista-login"); throw new Error("Sesión expirada: vuelve a ingresar."); }
  const texto = await r.text();
  const datos = texto ? JSON.parse(texto) : null;
  if (!r.ok) throw new Error(datos?.error ?? `Error ${r.status}`);
  return datos;
}

function mostrar(vista) {
  for (const v of ["vista-login", "vista-lista", "vista-caso"]) $(v).hidden = v !== vista;
}

async function cargarLista() {
  const casos = await api("GET", "/casos");
  const enviados = casos.filter((c) => c.estado === "Enviado").length;
  $("progreso").textContent = `Enviados: ${enviados} de ${casos.length}`;
  $("lista").replaceChildren(...casos.map((c) => {
    const li = document.createElement("li");
    const b = document.createElement("button");
    const estado = { Pendiente: "pendiente", EnCurso: "en curso", Enviado: "enviado" }[c.estado];
    b.textContent = `Expediente ${c.idExpediente} · ${estado}`;
    b.disabled = !c.habilitado;
    if (!c.habilitado) b.classList.add("secundario");
    b.onclick = () => abrir(c.idExpediente).catch((e) => alert(e.message));
    li.append(b);
    return li;
  }));
  mostrar("vista-lista");
}

async function abrir(id) {
  const caso = await api("POST", `/casos/${id}/abrir`);
  casoActual = id;
  $("titulo-caso").textContent = `Expediente ${id}`;
  $("narracion").textContent = caso.narracion;
  $("voucher").src = `/ficha/api/casos/${id}/voucher`;
  $("form-respuesta").reset();
  $("resultado-core").replaceChildren();
  $("error-caso").textContent = "";
  await pausar(caso.enPausa, false);
  clearInterval(latido);
  latido = setInterval(() => {
    if (casoActual && $("velo-pausa").hidden) api("POST", `/casos/${casoActual}/latido`).catch(() => {});
  }, caso.latidoSegundos * 1000);
  mostrar("vista-caso");
}

async function pausar(enPausa, avisar = true) {
  if (avisar) await api("POST", `/casos/${casoActual}/${enPausa ? "pausa" : "reanudar"}`);
  $("velo-pausa").hidden = !enPausa;
  $("contenido-caso").hidden = enPausa;
}

$("form-login").onsubmit = async (e) => {
  e.preventDefault();
  const f = new FormData(e.target);
  try {
    await api("POST", "/login", { codigo: f.get("codigo"), pin: f.get("pin") });
    $("sesion").textContent = `Analista ${f.get("codigo")}`;
    $("sesion").hidden = $("salir").hidden = false;
    $("error-login").textContent = "";
    await cargarLista();
  } catch {
    $("error-login").textContent = "Código o PIN incorrecto.";
  }
};

$("salir").onclick = async () => {
  clearInterval(latido);
  await api("POST", "/logout").catch(() => {});
  location.reload();
};

$("pausa").onclick = () => pausar(true).catch((e) => ($("error-caso").textContent = e.message));
$("reanudar").onclick = () => pausar(false).catch((e) => ($("error-caso").textContent = e.message));
$("volver").onclick = () => { clearInterval(latido); casoActual = null; cargarLista(); };

$("form-core").onsubmit = async (e) => {
  e.preventDefault();
  const codigo = new FormData(e.target).get("codigo");
  try {
    const r = await api("GET", `/core/transacciones?codigo=${encodeURIComponent(codigo)}`);
    const tabla = document.createElement("table");
    const cabecera = document.createElement("tr");
    for (const t of ["Operación", "Monto", "Fecha y hora", "Comercio", "Estado", "Aut. reforzada", "Dispositivo", "Riesgo"]) {
      const th = document.createElement("th");
      th.textContent = t;
      cabecera.append(th);
    }
    const thead = document.createElement("thead");
    thead.append(cabecera);
    const cuerpo = document.createElement("tbody");
    for (const m of r.movimientos) {
      const fila = document.createElement("tr");
      for (const v of [m.codigoOperacion, `${m.moneda} ${m.monto.toFixed(2)}`, m.fechaHora.replace("T", " "), m.comercio,
                       m.estado, m.autenticacionReforzada ? "sí" : "no", m.dispositivo ?? "—", m.indicadorRiesgo ? "sí" : "no"]) {
        const td = document.createElement("td");
        td.textContent = v;
        fila.append(td);
      }
      if (m.codigoOperacion === r.operacion) fila.style.fontWeight = "bold";
      cuerpo.append(fila);
    }
    tabla.append(thead, cuerpo);
    const nota = document.createElement("p");
    nota.textContent = `Dispositivo registrado del cliente: ${r.dispositivoRegistrado}` +
      (r.tipoCambio ? ` · Tipo de cambio del día: ${r.tipoCambio}` : "");
    $("resultado-core").replaceChildren(nota, tabla);
  } catch (err) {
    $("resultado-core").textContent = err.message;
  }
};

$("ver-politica").onclick = async () => {
  const p = $("politica");
  if (!p.hidden) { p.hidden = true; return; }
  const articulos = await api("GET", "/normas/politica");
  p.textContent = articulos.map((a) => a.texto).join("\n\n");
  p.hidden = false;
};

$("form-respuesta").onsubmit = async (e) => {
  e.preventDefault();
  const f = new FormData(e.target);
  const valor = (k) => (f.get(k) || "").trim() || null;
  try {
    await api("POST", `/casos/${casoActual}/enviar`, {
      intencion: valor("intencion"),
      monto: valor("monto") === null ? null : Number(valor("monto")),
      fecha: valor("fecha"),
      codigo: valor("codigo"),
      ruta: valor("ruta"),
      textoRespuesta: valor("textoRespuesta"),
    });
    clearInterval(latido);
    casoActual = null;
    await cargarLista();
  } catch (err) {
    $("error-caso").textContent = err.message;
  }
};

// Si la cookie sigue vigente, entra directo a la lista.
api("GET", "/casos").then(() => cargarLista()).catch(() => mostrar("vista-login"));
