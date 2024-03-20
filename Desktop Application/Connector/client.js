import websocketPlugin from "@fastify/websocket"
import InworldManager from "./inworld_manager.js"
import Fastify from 'fastify'
const fastify = Fastify({
    logger: false
});
fastify.register(websocketPlugin);

const PortPrefix = "--port="
const KeyPrefix = "--key="
const SecretPrefix = "--secret="
var PORT = 3030;
var KEY = "";
var SECRET = "";

process.argv.forEach(e => {
    if(e.includes(PortPrefix)){
        PORT = parseInt(e.replace(PortPrefix, ""))
    } else if(e.includes(KeyPrefix)){
        KEY = e.replace(KeyPrefix, "")
    } else if(e.includes(SecretPrefix)){
        SECRET = e.replace(SecretPrefix, "")
    } 
});

process.on('uncaughtException', function (err) {
    console.log('Caught exception: ', err);
});


// Main entry
fastify.get('/', async (request, reply) => {
    return "You shouldn't be seeing this. Dont try to hack the system! :)"
});

var clientManager; 
fastify.register(async function (fastify) {
    fastify.get('/connect', {
        websocket: true,
    }, (connection, req) => {
        connection.socket.on('message', message => {
            try {
                let msgObject = JSON.parse(message.toString());
                console.log(msgObject);
                if (msgObject.type == "connect") {
                    clientManager = new InworldManager(KEY, SECRET);
                    clientManager.connectToCharacter(msgObject.message, async (strData) => {
                        connection.socket.send(strData);
                    }, "friend");
                    connection.socket.send(JSON.stringify({ "type": "connected", "data": "Tadpole connection is completed. You can exchange thoughts with it now.." }));
                } else if (msgObject.type == "reconnect") {
                    let token = clientManager.getCurrentSessionToken()
                    clientManager.connectToCharacter(msgObject.message, async (strData) => {
                        connection.socket.send(strData);
                    }, "friend", token);
                    connection.socket.send(JSON.stringify({ "type": "reconnected", "data": "Reconnected" }));
                } else if (msgObject.type == "message") {
                    clientManager.sendMessage(msgObject.message);
                } else if (msgObject.type == "disconnect") {
                    clientManager.disconnectToCharacter();
                } else if (msgObject.type == "event") {
                    clientManager.sendEvent(msgObject.message, msgObject.parameters);
                }
            } catch {
                // Error parsing on JSON means it's base64 audio chunk
                if (clientManager) {
                    clientManager.sendAudioData(message);
                }
            }
        })
    })
});

// Run the server!
const StartEngine = async () => {
    try {
        console.log("Serving on port", PORT)
        await fastify.listen({
            port: PORT
        })
    } catch (err) {
        fastify.log.error(err);
        console.error(err);
        process.exit(1)
    }
}

console.log('Starting the engine..');
StartEngine();
