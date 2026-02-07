const AUTH_KEY = "Authorization";

async function setLogoutButton() {
    const cookie = await cookieStore.get(AUTH_KEY);
    if (cookie != null) {
        const logoutButton = document.getElementById("logout");
        console.log(logoutButton)
        console.log(cookie)
        const inner = document.createElement("button");
        inner.onclick = () => {
            window.location.replace("/logout")
        };
        inner.className = "navlink text-light btn";
        inner.innerHTML = "Logout"
        logoutButton.append(inner)
    }
}
setLogoutButton()
