const AUTH_KEY = "Authorization";

async function setLogoutButton() {
    const cookie = await cookieStore.get(AUTH_KEY);
    if (cookie != null) {
        console.log(logout)
        console.log(cookie)
        const inner = document.createElement("button");
        inner.onclick = () => {
            window.location.replace("/logout")
        };
        inner.className = "navlink text-light btn";
        inner.innerHTML = "Logout"
        logout.append(inner)
    }
}
_ = setLogoutButton()

async function decodeJWT(){
    const cookie = await cookieStore.get(AUTH_KEY);
    if(!cookie) return null
    const [, token] = decodeURI(cookie.value).split(" ");
    if(!token) return null;
    const [,payload, ] = token.split(".");
    if (!payload) return null;
    return JSON.parse(atob(payload));
}

async function setAdminPageLink(){
    const jwt = await decodeJWT();
    const isAdmin = jwt?.role === "Admin";
    if(!isAdmin) return;
    const inner = document.createElement("a");
    inner.className = "navlink text-light btn";
    inner.innerHTML = "Administration";
    inner.href = "/Admin";
    logout.prepend(inner);
}
_ = setAdminPageLink()
