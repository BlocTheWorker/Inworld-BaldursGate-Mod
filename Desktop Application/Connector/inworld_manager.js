import {
    InworldClient,
    SessionToken
} from '@inworld/nodejs-sdk';

const WORKSPACE_NAME = "baldursgatethree";

export default class InworldManager {
    constructor(key, secret) {
        this.key = key;
        this.secret = secret;
        this.client = new InworldClient().setApiKey({ key: key, secret: secret }).setConfiguration({
            connection: {
                disconnectTimeout: 3600 * 1000, // 1 hour
                autoReconnect: true,
            },
            capabilities: {
            audio: true,
            emotions: false,
            interruptions: true,
            narratedActions: false,
            phonemes: false,
            silence: true,
        }});
    }

    getCurrentSessionToken(){
        return this.sessionToken;
    }

    async connectToCharacter(id, callback, playerName = "friend", reconnectToken = null) {
        this.isAudioSessionStarted = false;
        this.connection = null;
        this.socketCallback = callback;
        let scene = "workspaces/" + WORKSPACE_NAME + "/characters/{CHARACTER_NAME}".replace("{CHARACTER_NAME}", id);
        this.client.setUser({
            fullName: playerName
        })
        this.client.setGenerateSessionToken(this.GenerateSessionTokenOverride.bind(this, reconnectToken));
        this.client.setScene(scene);
        this.client.setOnMessage(this.handler.bind(this));
        this.client.setOnError(async (err) => {
            if(err.details.includes("Conversation paused due to inactivity")){
                console.log("Conversation paused due to inactivity")
            } else {
                console.log("Error:", err)
            }
        });
        this.client.setOnDisconnect(async () => {
            let resp = { "type": "disconnected", "data": "" };
            this.socketCallback(JSON.stringify(resp))
            console.log("Disconnected..");
        })
        this.connection = this.client.build();
        await this.connection.sendAudioSessionStart();
        this.isAudioSessionStarted = true;
    }

    async GenerateSessionTokenOverride(stoken) {
        const token = await this.client.generateSessionToken();
        this.sessionToken = stoken ?? token.sessionId;
        const actualToken = new SessionToken({
            expirationTime: token.expirationTime,
            token: token.token,
            type: token.type,
            sessionId: this.sessionToken,
        });
        return actualToken;
    }

    disconnectToCharacter(){
        if (!this.connection || !this.connection.isActive()) return;
        this.connection.close();
        this.connection = null;
        this.isAudioSessionStarted = false;
    }

    sendEvent(event, params){
        if (!this.connection || !this.connection.isActive()) return;
        let triggerArr = []
        params.forEach( elem => {
            console.log(elem)
            triggerArr.push({
                name: elem["Key"],
                value: elem["Value"],
            })
        })
        this.connection.sendTrigger(event, triggerArr);
    }

    sendMessage(message){
        if (!this.connection || !this.connection.isActive()) return;
        this.connection.sendText(message);
    }

    sendAudioData(chunk){
        if (!this.connection || !this.connection.isActive()) return;
        if (!this.isAudioSessionStarted) return;
        this.connection.sendAudio(chunk);
    }

    async handler(msg){
        let resp = { "type": null, "data": ""};
        if(msg.type == 'AUDIO'){
            resp.type = msg.type 
            resp.data = msg.audio.chunk;
        } 
        else if(msg.isText()) {
            if(msg.routing.target.isPlayer){
                resp.type = "TEXT" 
                resp.data = msg.text.text;
            }
        }

        if (resp.type != null) {
            this.socketCallback(JSON.stringify(resp))
        }
    }

}