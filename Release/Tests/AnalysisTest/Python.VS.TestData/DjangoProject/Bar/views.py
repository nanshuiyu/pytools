from django.template import Context, loader
# Create your views here.
from django.http import HttpResponse

from Bar.models import Poll
from django.http import HttpResponse

def index(request):
    latest_poll_list = Poll.objects.all().order_by('-pub_date')[:5]
    t = loader.get_template('polls/index.html')
    c = Context({
        'latest_poll_list': latest_poll_list,
    })
    return HttpResponse(t.render(c))

def loop(request):
    t = loader.get_template('polls/loop.html')
    c = Context({
        'colors': ['red', 'blue', 'green']
    })
    return HttpResponse(t.render(c))

def loop_nobom(request):
    t = loader.get_template('polls/loop_nobom.html')
    c = Context({
        'colors': ['red', 'blue', 'green']
    })
    return HttpResponse(t.render(c))
