# About
The Multiplayer mod is a mod which enabled multiplayer (co-op) connections and functionality in Poly Bridge 2. Currently it fully support build mode.

# Mod Installation
Firstly, `BepInEx`, `PolyTechFramework` and `BepInEx.ConfigurationManager` need to be installed. then go to the [latest release](https://github.com/Conqu3red/PolyBridge2-Multiplayer-Mod/releases/latest) of this mod and download the dll file.
You need to put the dll file in
```
{PB2_INSTALL_DIRECTORY}/BepInEx/plugins
```

# Running the server
You will need Python 3.8+ installed to run the server.

```sh
# firstly clone the repo

git clone https://github.com/Conqu3red/PolyBridge2-Multiplayer-Mod.git

cd PolyBridge2-Multiplayer-Mod

# install the required python modules
pip install -r requirements.txt

# run the server
python server.py --localhost
```
For the server to be accessible by other people I reccomend using [ngrok](https://ngrok.com).

Once you have downloaded ngrok, follow it's instructions to connect to your account, then run
```bash
ngrok tcp 11000
```
It should boot up ngrok and there will be a url and port, you need to give this url and port to other people. (Note: you need to connect with the ip 127.0.0.1 and port 11000).
