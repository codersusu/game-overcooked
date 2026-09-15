mergeInto(LibraryManager.library, {
  BaraLiveStart: function(name, context, volume) {
    const objectName=UTF8ToString(name), snapshot=JSON.parse(UTF8ToString(context));
    const emit=data=>SendMessage(objectName,'OnLiveEvent',JSON.stringify(data));
    if(!window.BaraLive){emit({type:'error',message:'Open the game from its main index page to use live chat.'});return;}
    window.BaraLive.start(snapshot,emit,volume);
  },
  BaraLiveContext: function(context,volume) {if(window.BaraLive){window.BaraLive.volume(volume);window.BaraLive.update(JSON.parse(UTF8ToString(context)));}},
  BaraLiveStop: function() {if(window.BaraLive)window.BaraLive.stop();}
});
