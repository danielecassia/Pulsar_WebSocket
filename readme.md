Configurando o Pulsar para suportar WebSocket usando o guia em https://pulsar.apache.org/docs/en/client-libraries-websocket/

Crie o container abaixo:

```
docker run -it \
  -p 6650:6650 \
  -p 8080:8080 \
  --mount source=pulsardata,target=/pulsar/data \
  --mount source=pulsarconf,target=/pulsar/conf \
  apachepulsar/pulsar:2.7.1 \
  bin/pulsar standalone
```


Execute `docker exec -it <id do container> /bin/bash` para editar o arquivo de configuração broker.conf para ativar o web socket conforme citado no link acima.

